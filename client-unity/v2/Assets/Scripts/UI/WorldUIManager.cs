using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UIElements;
using VictoriaLike.Client.Api;

namespace VictoriaLike.Client.UI
{
    [RequireComponent(typeof(UIDocument))]
    public class WorldUIManager : MonoBehaviour
    {
        [SerializeField] private WorldWebSocketClient wsClient;
        [SerializeField] private string serverUrl = "http://localhost:5001";

        private IWorldApiClient _apiClient;
        private IWorldCommandService _commandService;
        private IClientCommandScheduler _commandScheduler;
        private UIDocument _document;
        private VisualElement _root;

        private Label _tickLabel;
        private Label _dateLabel;
        private Label _connectionLabel;
        private Label _userLabel;
        private Label _reformPressureLabel;
        private Label _countryNameLabel;
        private Label _countryStatsLabel;
        private Label _eventFeedSummaryLabel;
        private Label _popSummaryLabel;
        private Label _budgetSummaryLabel;
        private Label _budgetEffectsLabel;
        private Label _flatTaxStateLabel;
        private Label _poorTaxStateLabel;
        private Label _middleTaxStateLabel;
        private Label _richTaxStateLabel;
        private Label _educationStateLabel;
        private Label _militaryStateLabel;
        private Label _administrationStateLabel;
        private Label _provinceTitleLabel;
        private Label _provinceInfoLabel;
        private Label _constructionOptionsLabel;
        private Label _constructionLabel;
        private Label _commandStatusLabel;
        private Label _militarySummaryLabel;
        private Label _militarySelectionLabel;
        private Label _warSummaryLabel;
        private TextField _provinceSearchField;
        private ScrollView _eventFeedList;
        private ScrollView _marketList;
        private ScrollView _provinceList;
        private ScrollView _armyList;

        private string _controlledCountryId;
        private string _controlledCountryName;
        private string _sort = "name";
        private string _order = "asc";
        private string _filterOwnerId;
        private ProvinceData _selectedProvince;
        private ArmyStackData _selectedArmy;
        private AdminCountryInspectorData _lastInspector;
        private readonly Dictionary<string, float> _scheduledValues = new();
        private readonly Dictionary<string, Label> _scheduledStateLabels = new();
        private readonly Dictionary<string, Button[]> _scheduledButtons = new();
        private readonly Dictionary<string, ProvinceData> _knownProvincesById = new();
        private readonly Dictionary<string, CountryData> _knownCountriesById = new();
        private List<ArmyStackData> _lastArmies = new();
        private List<WarData> _lastWars = new();
        private readonly HashSet<string> _pendingBuildCommandKeys = new();
        private readonly Dictionary<string, Button> _buildButtons = new();
        private readonly Dictionary<string, string> _buildButtonDefaultTexts = new();
        private readonly HashSet<string> _serverUnavailableBuildTypes = new();
        private Button _moveArmyButton;
        private Button _declareWarButton;
        private Button _makePeaceButton;
        private string _selectedProvinceBuildStatus;
        private WsConnectionState _lastConnectionState = WsConnectionState.Disconnected;
        private long _lastEventFeedRefreshTick;

        private string _popGroupBy = "type";
        private string _popFilterType;
        private string _popFilterCulture;
        private string _popFilterReligion;
        private string _popFilterStrata;
        private DropdownField _popFilterTypeDropdown;
        private DropdownField _popFilterCultureDropdown;
        private DropdownField _popFilterReligionDropdown;
        private DropdownField _popFilterStrataDropdown;
        private readonly Dictionary<string, Button> _popGroupByButtons = new();
        private const string PopFilterAll = "All";

        private static readonly string[] DashboardTabs = { "production", "budget", "technology", "politics", "population", "trade", "diplomacy", "military" };
        private string _activeTab = "production";
        private readonly Dictionary<string, Button> _tabButtons = new();
        private Label _politicsSummaryLabel;
        private Label _politicsDetailLabel;

        private void Awake()
        {
            _apiClient = new WorldApiClient(serverUrl);
            _commandService = new WorldCommandService(_apiClient, wsClient);
            _commandScheduler = new ClientCommandScheduler();
            _document = GetComponent<UIDocument>();
        }

        private void OnEnable()
        {
            BindDocument();
            BindWebSocket();
        }

        private void OnDisable()
        {
            UnbindWebSocket();
            _commandScheduler?.Clear();
        }

        public async Task FetchInitialSnapshotAsync()
        {
            if (_root == null)
                BindDocument();

            _controlledCountryId = PlayerSession.ControlledCountryId;
            _userLabel.text = string.IsNullOrEmpty(PlayerSession.Username) ? "User: -" : $"User: {PlayerSession.Username}";

            try
            {
                var summary = await _apiClient.GetWorldSummaryAsync();
                _tickLabel.text = $"Tick: {summary.tick}";
                _dateLabel.text = summary.world_date;

                var countries = await _apiClient.ListCountriesAsync();
                RememberCountries(countries);
                var controlled = countries.FirstOrDefault(c => c.id == _controlledCountryId) ?? countries.FirstOrDefault();
                if (controlled != null)
                {
                    _controlledCountryId = controlled.id;
                    _controlledCountryName = controlled.name;
                    RenderCountry(controlled.name, controlled.tax_rate, controlled.treasury);
                }

                await RefreshCountryInspectorAsync();
                await RefreshProvincesAsync();
                await RefreshMilitaryAsync();
                await RefreshEventFeedAsync();
            }
            catch (Exception ex)
            {
                Debug.LogError($"[WorldUI] Snapshot fetch failed: {ex.Message}");
                SetCommandStatus($"Snapshot failed: {ex.Message}");
            }
        }

        private void BindDocument()
        {
            _root = _document.rootVisualElement;
            if (_root == null)
                return;

            _tickLabel = Required<Label>("tick-label");
            _dateLabel = Required<Label>("date-label");
            _connectionLabel = Required<Label>("connection-label");
            _userLabel = Required<Label>("user-label");
            _reformPressureLabel = Required<Label>("reform-pressure-label");
            _countryNameLabel = Required<Label>("country-name-label");
            _countryStatsLabel = Required<Label>("country-stats-label");
            _eventFeedSummaryLabel = Required<Label>("event-feed-summary-label");
            _popSummaryLabel = Required<Label>("pop-summary-label");
            _budgetSummaryLabel = Required<Label>("budget-summary-label");
            _budgetEffectsLabel = Required<Label>("budget-effects-label");
            _flatTaxStateLabel = Required<Label>("flat-tax-state-label");
            _poorTaxStateLabel = Required<Label>("poor-tax-state-label");
            _middleTaxStateLabel = Required<Label>("middle-tax-state-label");
            _richTaxStateLabel = Required<Label>("rich-tax-state-label");
            _educationStateLabel = Required<Label>("education-state-label");
            _militaryStateLabel = Required<Label>("military-state-label");
            _administrationStateLabel = Required<Label>("administration-state-label");
            _provinceTitleLabel = Required<Label>("province-title-label");
            _provinceInfoLabel = Required<Label>("province-info-label");
            _constructionOptionsLabel = Required<Label>("construction-options-label");
            _constructionLabel = Required<Label>("construction-label");
            _commandStatusLabel = Required<Label>("command-status-label");
            _militarySummaryLabel = Required<Label>("military-summary-label");
            _militarySelectionLabel = Required<Label>("military-selection-label");
            _warSummaryLabel = Required<Label>("war-summary-label");
            _provinceSearchField = Required<TextField>("province-search-field");
            _eventFeedList = Required<ScrollView>("event-feed-list");
            _marketList = Required<ScrollView>("market-list");
            _provinceList = Required<ScrollView>("province-list");
            _armyList = Required<ScrollView>("army-list");

            _scheduledStateLabels.Clear();
            _scheduledStateLabels[FlatTaxFieldKey] = _flatTaxStateLabel;
            _scheduledStateLabels[GetStrataTaxFieldKey("poor")] = _poorTaxStateLabel;
            _scheduledStateLabels[GetStrataTaxFieldKey("middle")] = _middleTaxStateLabel;
            _scheduledStateLabels[GetStrataTaxFieldKey("rich")] = _richTaxStateLabel;
            _scheduledStateLabels[GetSpendingFieldKey("education")] = _educationStateLabel;
            _scheduledStateLabels[GetSpendingFieldKey("military")] = _militaryStateLabel;
            _scheduledStateLabels[GetSpendingFieldKey("administration")] = _administrationStateLabel;

            _scheduledButtons.Clear();
            _scheduledButtons[FlatTaxFieldKey] = new[] { Button("flat-tax-down-button"), Button("flat-tax-up-button") };
            _scheduledButtons[GetStrataTaxFieldKey("poor")] = new[] { Button("poor-tax-down-button"), Button("poor-tax-up-button") };
            _scheduledButtons[GetStrataTaxFieldKey("middle")] = new[] { Button("middle-tax-down-button"), Button("middle-tax-up-button") };
            _scheduledButtons[GetStrataTaxFieldKey("rich")] = new[] { Button("rich-tax-down-button"), Button("rich-tax-up-button") };
            _scheduledButtons[GetSpendingFieldKey("education")] = new[] { Button("education-down-button"), Button("education-up-button") };
            _scheduledButtons[GetSpendingFieldKey("military")] = new[] { Button("military-down-button"), Button("military-up-button") };
            _scheduledButtons[GetSpendingFieldKey("administration")] = new[] { Button("administration-down-button"), Button("administration-up-button") };

            _buildButtons.Clear();
            _buildButtons["farm"] = Button("build-farm-button");
            _buildButtons["mine"] = Button("build-mine-button");
            _buildButtons["workshop"] = Button("build-workshop-button");

            _buildButtonDefaultTexts.Clear();
            foreach (var kv in _buildButtons)
                _buildButtonDefaultTexts[kv.Key] = kv.Value.text;

            Button("refresh-button").clicked += () => _ = RefreshAllAsync();
            Button("explain-country-button").clicked += () => _ = ExplainControlledCountryBudgetAsync();
            Button("explain-budget-button").clicked += () => _ = ExplainControlledCountryBudgetAsync();
            Button("explain-province-button").clicked += () => _ = ExplainSelectedProvinceAsync();
            Button("explain-war-button").clicked += () => _ = ExplainActiveWarAsync();
            Button("filter-mine-button").clicked += () => { _filterOwnerId = PlayerSession.ControlledCountryId; _ = RefreshProvincesAsync(); };
            Button("clear-filter-button").clicked += () => { _filterOwnerId = null; _ = RefreshProvincesAsync(); };
            Button("sort-name-button").clicked += () => { ToggleSort("name"); _ = RefreshProvincesAsync(); };
            Button("sort-population-button").clicked += () => { ToggleSort("population"); _ = RefreshProvincesAsync(); };
            _provinceSearchField.RegisterValueChangedCallback(evt => { _ = RefreshProvincesAsync(); });

            Button("flat-tax-down-button").clicked += () => _ = ChangeFlatTaxAsync(-0.05f);
            Button("flat-tax-up-button").clicked += () => _ = ChangeFlatTaxAsync(0.05f);
            Button("poor-tax-down-button").clicked += () => _ = ChangeStrataTaxAsync("poor", -0.05f);
            Button("poor-tax-up-button").clicked += () => _ = ChangeStrataTaxAsync("poor", 0.05f);
            Button("middle-tax-down-button").clicked += () => _ = ChangeStrataTaxAsync("middle", -0.05f);
            Button("middle-tax-up-button").clicked += () => _ = ChangeStrataTaxAsync("middle", 0.05f);
            Button("rich-tax-down-button").clicked += () => _ = ChangeStrataTaxAsync("rich", -0.05f);
            Button("rich-tax-up-button").clicked += () => _ = ChangeStrataTaxAsync("rich", 0.05f);
            Button("education-down-button").clicked += () => _ = ChangeSpendingAsync("education", -0.10f);
            Button("education-up-button").clicked += () => _ = ChangeSpendingAsync("education", 0.10f);
            Button("military-down-button").clicked += () => _ = ChangeSpendingAsync("military", -0.10f);
            Button("military-up-button").clicked += () => _ = ChangeSpendingAsync("military", 0.10f);
            Button("administration-down-button").clicked += () => _ = ChangeSpendingAsync("administration", -0.10f);
            Button("administration-up-button").clicked += () => _ = ChangeSpendingAsync("administration", 0.10f);

            Button("build-farm-button").clicked += () => _ = QueueBuildingAsync("farm");
            Button("build-mine-button").clicked += () => _ = QueueBuildingAsync("mine");
            Button("build-workshop-button").clicked += () => _ = QueueBuildingAsync("workshop");
            Button("refresh-military-button").clicked += () => _ = RefreshMilitaryAsync();
            _moveArmyButton = Button("move-army-button");
            _declareWarButton = Button("declare-war-button");
            _makePeaceButton = Button("make-peace-button");
            _moveArmyButton.clicked += () => _ = MoveSelectedArmyAsync();
            _declareWarButton.clicked += () => _ = DeclareWarFromSelectionAsync();
            _makePeaceButton.clicked += () => _ = MakePeaceFromSelectionAsync();

            BindPopulationSlicer();
            BindDashboardTabs();

            ResetBudgetFieldStateRows();
            RefreshBuildButtons();
            RefreshMilitaryActionState();
            RenderConnection(wsClient != null ? wsClient.ConnectionState : WsConnectionState.Disconnected);
        }

        private void BindWebSocket()
        {
            _commandService?.BindRealtime();
            _commandScheduler.StatusChanged += HandleScheduledCommandStatusChanged;

            if (wsClient == null)
                return;

            wsClient.OnConnectionStateChanged += HandleConnectionStateChanged;
            wsClient.OnCountryUpdate += HandleCountryUpdate;
            wsClient.OnMarketUpdate += HandleMarketUpdate;
            _commandService.CommandOutcomeReceived += HandleCommandOutcome;
        }

        private void UnbindWebSocket()
        {
            _commandService?.UnbindRealtime();
            _commandScheduler.StatusChanged -= HandleScheduledCommandStatusChanged;

            if (wsClient == null)
                return;

            wsClient.OnConnectionStateChanged -= HandleConnectionStateChanged;
            wsClient.OnCountryUpdate -= HandleCountryUpdate;
            wsClient.OnMarketUpdate -= HandleMarketUpdate;
            _commandService.CommandOutcomeReceived -= HandleCommandOutcome;
        }

        private async Task RefreshAllAsync()
        {
            await RefreshCountryInspectorAsync();
            await RefreshProvincesAsync();
            await RefreshMilitaryAsync();
            await RefreshEventFeedAsync();
            if (_selectedProvince != null)
                await SelectProvinceAsync(_selectedProvince);
        }

        private async Task RefreshCountryInspectorAsync()
        {
            if (!EnsureCountrySelected())
                return;

            try
            {
                var inspector = await _apiClient.GetCountryInspectorAsync(_controlledCountryId);
                _lastInspector = inspector;
                RenderInspector(inspector);
                await RefreshBudgetPreviewBaselineAsync();
            }
            catch (Exception ex)
            {
                Debug.LogError($"[WorldUI] Country inspector fetch failed: {ex.Message}");
                SetCommandStatus($"Inspector failed: {ex.Message}");
            }
        }

        private async Task RefreshProvincesAsync()
        {
            try
            {
                var provinces = await _apiClient.ListProvincesAsync(_filterOwnerId, _sort, _order);
                RenderProvinces(provinces);
                RememberProvinces(provinces);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[WorldUI] Province fetch failed: {ex.Message}");
                SetCommandStatus($"Province fetch failed: {ex.Message}");
            }
        }

        private async Task RefreshMilitaryAsync()
        {
            if (_armyList == null || !EnsureCountrySelected())
                return;

            try
            {
                _lastArmies = await _apiClient.ListArmiesAsync(_controlledCountryId) ?? new List<ArmyStackData>();
                _lastWars = await _apiClient.ListWarsAsync() ?? new List<WarData>();

                if (_selectedArmy != null)
                    _selectedArmy = _lastArmies.FirstOrDefault(army => army.id == _selectedArmy.id);

                RenderMilitary();
            }
            catch (Exception ex)
            {
                Debug.LogError($"[WorldUI] Military fetch failed: {ex.Message}");
                _militarySummaryLabel.text = "Military unavailable.";
                SetCommandStatus($"Military fetch failed: {ex.Message}");
            }
        }

        private void RenderCountry(string name, int taxRate, float treasury)
        {
            _countryNameLabel.text = string.IsNullOrEmpty(name) ? "-" : name;
            _countryStatsLabel.text = $"Treasury: £{treasury:N0}\nTax: {taxRate}%";
        }

        private void RenderInspector(AdminCountryInspectorData inspector)
        {
            RefreshPopFilterOptions(inspector);
            _popSummaryLabel.text = BuildPopulationSummaryText(inspector);
            RenderReformPressure(inspector.reform_pressure);
            if (_activeTab == "politics")
                RenderPoliticsView(inspector);

            var warnings = "";
            if (inspector.market_warnings != null && inspector.market_warnings.Count > 0)
            {
                warnings = "\nWarnings:";
                foreach (var warning in inspector.market_warnings)
                    warnings += $"\n[{warning.severity}] {warning.message}";
            }

            _budgetSummaryLabel.text =
                $"Treasury: £{inspector.treasury:N0}  Provinces: {inspector.province_count}\n" +
                $"Tax flat: {inspector.tax_rate}%  poor {FormatStrataTax(inspector.poor_tax_rate, inspector.tax_rate)}  mid {FormatStrataTax(inspector.middle_tax_rate, inspector.tax_rate)}  rich {FormatStrataTax(inspector.rich_tax_rate, inspector.tax_rate)}\n" +
                $"Spending  edu {inspector.education_spending:P0}  mil {inspector.military_spending:P0}  adm {inspector.administration_spending:P0}" +
                warnings;

            RefreshIdleBudgetFieldStateRows(inspector);
        }

        private void RenderProvinces(List<ProvinceData> provinces)
        {
            _provinceList.Clear();
            var search = _provinceSearchField == null ? null : _provinceSearchField.value;
            var visibleProvinces = string.IsNullOrWhiteSpace(search)
                ? provinces
                : provinces
                    .Where(province =>
                        ContainsSearch(province.name, search) ||
                        ContainsSearch(province.owner_name, search) ||
                        ContainsSearch(province.rgo_type, search))
                    .ToList();

            if (visibleProvinces == null || visibleProvinces.Count == 0)
            {
                var empty = new Label("No matching provinces.");
                empty.AddToClassList("province-meta");
                _provinceList.Add(empty);
                return;
            }

            foreach (var province in visibleProvinces)
            {
                var row = new VisualElement();
                row.AddToClassList("province-row");
                if (_selectedProvince != null && _selectedProvince.id == province.id)
                    row.AddToClassList("province-row-selected");

                var name = new Label(province.name);
                name.AddToClassList("province-name");
                var meta = new Label($"{province.owner_name}  Pop {province.population:N0}");
                meta.AddToClassList("province-meta");
                row.Add(name);
                row.Add(meta);
                row.RegisterCallback<ClickEvent>(evt => _ = SelectProvinceAsync(province));
                _provinceList.Add(row);
            }
        }

        private static bool ContainsSearch(string value, string search) =>
            !string.IsNullOrEmpty(value) &&
            value.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0;

        private void RenderMilitary()
        {
            if (_armyList == null || _militarySummaryLabel == null || _warSummaryLabel == null)
                return;

            _armyList.Clear();

            if (_lastArmies == null || _lastArmies.Count == 0)
            {
                _militarySummaryLabel.text = "No armies for selected country.";
            }
            else
            {
                var totalSoldiers = _lastArmies.Sum(army => army.soldier_count);
                var moving = _lastArmies.Count(army => army.is_moving);
                _militarySummaryLabel.text = $"Armies {_lastArmies.Count}  Soldiers {totalSoldiers:N0}  Moving {moving}";
            }

            foreach (var army in (_lastArmies ?? new List<ArmyStackData>()).Where(army => army != null))
            {
                var row = new VisualElement();
                row.AddToClassList("army-row");
                if (_selectedArmy != null && _selectedArmy.id == army.id)
                    row.AddToClassList("army-row-selected");

                var title = new Label($"{ShortId(army.id)}  {army.soldier_count:N0} soldiers");
                title.AddToClassList("army-name");
                var destination = army.is_moving
                    ? $" -> {army.destination_province_name ?? army.destination_province_id} ({army.movement_ticks_remaining} ticks)"
                    : string.Empty;
                var meta = new Label($"{army.location_province_name}{destination}  Morale {army.morale:P0}");
                meta.AddToClassList("army-meta");
                row.Add(title);
                row.Add(meta);
                row.RegisterCallback<ClickEvent>(evt =>
                {
                    _selectedArmy = army;
                    RenderMilitary();
                    RefreshMilitaryActionState();
                    SetCommandStatus($"Selected army {ShortId(army.id)}.");
                });
                _armyList.Add(row);
            }

            var relevantWars = (_lastWars ?? new List<WarData>())
                .Where(war => war != null && IsWarRelevant(war))
                .ToList();
            if (relevantWars.Count == 0)
            {
                _warSummaryLabel.text = "Wars: none involving selected country.";
                RefreshMilitaryActionState();
                return;
            }

            _warSummaryLabel.text = "Wars:\n" + string.Join(
                "\n",
                relevantWars.Take(3).Select(war =>
                    $"{(war.is_active ? "Active" : "Ended")}: {war.attacker_country_name} vs {war.defender_country_name}"));
            RefreshMilitaryActionState();
        }

        private async Task RefreshEventFeedAsync(long observedTick = 0)
        {
            if (_eventFeedList == null || string.IsNullOrEmpty(_controlledCountryId))
                return;

            try
            {
                var events = await _apiClient.GetEventFeedAsync(_controlledCountryId, 30);
                if (observedTick > 0)
                    _lastEventFeedRefreshTick = observedTick;
                else if (wsClient != null && wsClient.LastTickSeen > 0)
                    _lastEventFeedRefreshTick = wsClient.LastTickSeen;

                RenderEventFeed(events);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[WorldUI] Event feed failed: {ex.Message}");
                _eventFeedSummaryLabel.text = "Event feed unavailable.";
            }
        }

        private void RenderEventFeed(List<WorldEventData> events)
        {
            _eventFeedList.Clear();

            if (events == null || events.Count == 0)
            {
                _eventFeedSummaryLabel.text = "No current warnings.";
                return;
            }

            var critical = events.Count(e => e != null && e.severity == "critical");
            var warnings = events.Count(e => e != null && e.severity == "warn");
            var info = events.Count(e => e != null && e.severity == "info");
            _eventFeedSummaryLabel.text = $"Digest: {critical} critical  {warnings} warning  {info} info";

            foreach (var item in events.Where(e => e != null))
            {
                var row = new VisualElement();
                row.AddToClassList("event-row");
                row.AddToClassList($"event-row-{NormalizeEventSeverity(item.severity)}");

                var title = new Label($"{NormalizeEventSeverity(item.severity).ToUpperInvariant()} · {item.title}");
                title.AddToClassList("event-title");
                var message = new Label(item.message);
                message.AddToClassList("event-message");
                var whyButton = new Button(() => { _ = ExplainEventAsync(item); }) { text = "Why?" };
                whyButton.AddToClassList("button");
                whyButton.AddToClassList("secondary");
                whyButton.AddToClassList("small");
                whyButton.AddToClassList("event-why-button");
                whyButton.tooltip = "Explain why this changed";
                whyButton.RegisterCallback<ClickEvent>(evt => evt.StopPropagation());
                row.Add(title);
                row.Add(message);
                row.Add(whyButton);
                row.RegisterCallback<ClickEvent>(evt => { _ = NavigateFromEventAsync(item); });
                _eventFeedList.Add(row);
            }
        }

        private async Task ExplainEventAsync(WorldEventData item)
        {
            if (item == null)
                return;

            try
            {
                ExplanationData explanation = null;
                if (!string.IsNullOrEmpty(item.good_id))
                {
                    explanation = await _apiClient.ExplainGoodAsync(item.good_id);
                }
                else if (item.target_panel == "budget" && !string.IsNullOrEmpty(_controlledCountryId))
                {
                    explanation = await _apiClient.ExplainCountryBudgetAsync(_controlledCountryId);
                }
                else if (item.target_panel == "province" && !string.IsNullOrEmpty(item.province_id))
                {
                    explanation = await _apiClient.ExplainProvinceEmploymentAsync(item.province_id);
                }
                else if (item.target_panel == "country" && !string.IsNullOrEmpty(item.country_id))
                {
                    explanation = await _apiClient.ExplainCountryBudgetAsync(item.country_id);
                }
                else if (item.id != null && item.id.StartsWith("war:active:", StringComparison.Ordinal))
                {
                    explanation = await _apiClient.ExplainWarAsync(item.id.Substring("war:active:".Length));
                }

                if (explanation != null)
                    SetCommandStatus(FormatExplanation(explanation));
                else
                    SetCommandStatus($"No direct explanation is available for {item.title}.");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[WorldUI] Explanation failed: {ex.Message}");
                SetCommandStatus($"Explanation failed: {ex.Message}");
            }
        }

        private async Task NavigateFromEventAsync(WorldEventData item)
        {
            if (item == null)
                return;

            if (!string.IsNullOrEmpty(item.province_id))
            {
                if (_knownProvincesById.TryGetValue(item.province_id, out var knownProvince))
                {
                    await SelectProvinceAsync(knownProvince);
                    return;
                }

                var provinces = await _apiClient.ListProvincesAsync();
                RememberProvinces(provinces);
                var province = provinces.FirstOrDefault(p => p.id == item.province_id);
                if (province != null)
                {
                    await SelectProvinceAsync(province);
                    return;
                }
            }

            if (item.target_panel == "country" || item.target_panel == "budget" || item.target_panel == "population")
            {
                await RefreshCountryInspectorAsync();
                SetCommandStatus($"Selected event: {item.title}");
                return;
            }

            if (item.target_panel == "market")
            {
                SetCommandStatus($"Selected market event: {item.title}");
                return;
            }

            SetCommandStatus($"Selected event: {item.title}");
        }

        private static string NormalizeEventSeverity(string severity)
        {
            if (severity == "critical" || severity == "warn" || severity == "info")
                return severity;
            return "info";
        }

        private static string FormatExplanation(ExplanationData explanation)
        {
            if (explanation == null)
                return string.Empty;

            var parts = new List<string>();
            if (!string.IsNullOrEmpty(explanation.summary))
                parts.Add(explanation.summary);

            if (explanation.factors != null && explanation.factors.Count > 0)
            {
                var factorText = explanation.factors
                    .Where(f => f != null && !string.IsNullOrEmpty(f.label))
                    .Take(3)
                    .Select(f => string.IsNullOrEmpty(f.detail) ? f.label : $"{f.label}: {f.detail}");
                parts.Add(string.Join(" | ", factorText));
            }

            return parts.Count > 0 ? string.Join(" ", parts) : explanation.title;
        }

        private void RememberProvinces(List<ProvinceData> provinces)
        {
            if (provinces == null)
                return;

            foreach (var province in provinces.Where(p => p != null && !string.IsNullOrEmpty(p.id)))
                _knownProvincesById[province.id] = province;
        }

        private void RememberCountries(List<CountryData> countries)
        {
            _knownCountriesById.Clear();
            if (countries == null)
                return;

            foreach (var country in countries.Where(c => c != null && !string.IsNullOrEmpty(c.id)))
                _knownCountriesById[country.id] = country;
        }

        private async Task SelectProvinceAsync(ProvinceData province)
        {
            _selectedProvince = province;
            _selectedProvinceBuildStatus = null;
            _serverUnavailableBuildTypes.Clear();
            RefreshBuildButtons();
            RefreshMilitaryActionState();

            try
            {
                var detail = await _apiClient.GetProvinceDetailAsync(province.id);
                var inspector = await _apiClient.GetProvinceInspectorAsync(province.id);

                _provinceTitleLabel.text = detail.name;
                var info =
                    $"Owner: {detail.owner_name}\n" +
                    $"Market: {detail.market_name}\n" +
                    $"Population: {detail.population:N0}\n" +
                    $"RGO: {inspector.rgo_type}  Workforce: {inspector.workforce:N0}  Needs: {inspector.needs_fulfillment:P0}";

                if (inspector.pop_groups != null && inspector.pop_groups.Count > 0)
                {
                    info += "\n\nPOPs:";
                    foreach (var pop in inspector.pop_groups
                        .Where(p => p != null)
                        .OrderByDescending(p => p.size))
                    {
                        var employed = pop.size > 0 ? (float)pop.employed_count / pop.size : 0f;
                        info +=
                            $"\n  {pop.pop_type} ({pop.strata}): {pop.size:N0} ({pop.population_share:P0})" +
                            $"  {pop.culture}/{pop.religion}" +
                            $"  emp {employed:P0}  lit {pop.literacy:P0}  mil {pop.militancy:F2}" +
                            $"  life {pop.life_needs_fulfillment:P0}  ev {pop.everyday_needs_fulfillment:P0}  lux {pop.luxury_needs_fulfillment:P0}";
                    }
                }

                var outputs = FormatDictionary(inspector.outputs_per_tick);
                if (!string.IsNullOrEmpty(outputs))
                    info += $"\n\nOutput/tick: {outputs}";

                var demand = FormatDictionary(inspector.local_demand);
                if (!string.IsNullOrEmpty(demand))
                    info += $"\nLocal demand: {demand}";

                if (inspector.factories != null && inspector.factories.Count > 0)
                {
                    info += "\n\nFactories:";
                    foreach (var factory in inspector.factories.Where(f => f != null))
                    {
                        var inputs = FormatDictionary(factory.input_goods);
                        info +=
                            $"\n  {factory.type} L{factory.level}: {factory.output_good} {factory.output_per_tick:F2}/tick" +
                            $"  workers {factory.employed_craftsmen + factory.employed_clerks:N0}" +
                            $"  profit £{factory.profit_last_tick:F1}" +
                            (string.IsNullOrEmpty(inputs) ? string.Empty : $"  inputs {inputs}");
                    }
                }

                _provinceInfoLabel.text = info;

                await RenderConstructionAsync(province.id);
                await RefreshConstructionOptionsAsync(province.id);
                await RefreshProvincesAsync();
            }
            catch (Exception ex)
            {
                Debug.LogError($"[WorldUI] Province detail failed: {ex.Message}");
                SetCommandStatus($"Province detail failed: {ex.Message}");
            }
        }

        private async Task RenderConstructionAsync(string provinceId)
        {
            var queue = await _apiClient.GetBuildingQueueAsync();
            var matching = queue.Where(item => item.province_id == provinceId).ToList();
            if (matching.Count == 0 && string.IsNullOrEmpty(_selectedProvinceBuildStatus))
            {
                _constructionLabel.text = "Construction: none";
                return;
            }

            var lines = new List<string>();
            if (!string.IsNullOrEmpty(_selectedProvinceBuildStatus))
                lines.Add(_selectedProvinceBuildStatus);

            lines.AddRange(matching.Select(item => $"{item.building_type}: {item.ticks_remaining} ticks"));
            _constructionLabel.text = "Construction:\n" + string.Join("\n", lines);
        }

        private void HandleMarketUpdate(MarketUpdateData data)
        {
            if (data.Tick > 0)
                _tickLabel.text = $"Tick: {data.Tick}";

            if (data.Tick > 0 && data.Tick - _lastEventFeedRefreshTick >= 15)
                _ = RefreshEventFeedAsync(data.Tick);

            _marketList.Clear();
            foreach (var kv in data.Prices.OrderBy(kv => kv.Key))
            {
                var row = new VisualElement();
                row.AddToClassList("market-row");
                var supply = data.Supply.TryGetValue(kv.Key, out var s) ? s : 0f;
                var demand = data.Demand.TryGetValue(kv.Key, out var d) ? d : 0f;
                row.Add(new Label(kv.Key));
                row.Add(new Label($"{kv.Value:F2}  S:{supply:F1} D:{demand:F1}"));
                var goodId = kv.Key;
                var whyButton = new Button(() => { _ = ExplainGoodAsync(goodId); }) { text = "Why?" };
                whyButton.AddToClassList("button");
                whyButton.AddToClassList("secondary");
                whyButton.AddToClassList("small");
                whyButton.AddToClassList("market-why-button");
                whyButton.tooltip = $"Explain {goodId} price";
                row.Add(whyButton);
                _marketList.Add(row);
            }
        }

        private async Task ExplainGoodAsync(string goodId)
        {
            if (string.IsNullOrEmpty(goodId))
                return;
            await PresentExplanationAsync(
                () => _apiClient.ExplainGoodAsync(goodId),
                $"No explanation available for {goodId}.");
        }

        private async Task ExplainControlledCountryBudgetAsync()
        {
            if (string.IsNullOrEmpty(_controlledCountryId))
            {
                SetCommandStatus("Select a country before requesting an explanation.");
                return;
            }

            await PresentExplanationAsync(
                () => _apiClient.ExplainCountryBudgetAsync(_controlledCountryId),
                "No budget explanation available.");
        }

        private async Task ExplainSelectedProvinceAsync()
        {
            if (_selectedProvince == null || string.IsNullOrEmpty(_selectedProvince.id))
            {
                SetCommandStatus("Select a province to see why employment looks the way it does.");
                return;
            }

            await PresentExplanationAsync(
                () => _apiClient.ExplainProvinceEmploymentAsync(_selectedProvince.id),
                "No province explanation available.");
        }

        private async Task ExplainActiveWarAsync()
        {
            var war = _lastWars?.FirstOrDefault(w => w != null && w.is_active && (
                w.attacker_country_id == _controlledCountryId ||
                w.defender_country_id == _controlledCountryId))
                ?? _lastWars?.FirstOrDefault(w => w != null && w.is_active);
            if (war == null || string.IsNullOrEmpty(war.id))
            {
                SetCommandStatus("No active war to explain.");
                return;
            }

            await PresentExplanationAsync(
                () => _apiClient.ExplainWarAsync(war.id),
                "No war explanation available.");
        }

        private async Task PresentExplanationAsync(Func<Task<ExplanationData>> fetch, string fallback)
        {
            try
            {
                var explanation = await fetch();
                SetCommandStatus(explanation != null ? FormatExplanation(explanation) : fallback);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[WorldUI] Explanation failed: {ex.Message}");
                SetCommandStatus($"Explanation failed: {ex.Message}");
            }
        }

        private void HandleCountryUpdate(CountryUpdateData data)
        {
            if (data.Tick > 0)
                _tickLabel.text = $"Tick: {data.Tick}";
            if (data.CountryId != _controlledCountryId)
                return;

            RenderCountry(_controlledCountryName, data.TaxRate, data.Treasury);
        }

        private void HandleCommandOutcome(CommandOutcomeData data)
        {
            SetCommandStatus(CommandOutcomeMapper.Format(data));
            if (data != null && data.Source == CommandOutcomeSource.WebSocketEvent)
                _ = RefreshAllAsync();
        }

        private void RenderConnection(WsConnectionState state)
        {
            if (_connectionLabel == null)
                return;

            _connectionLabel.text = $"WS: {state}";
            _connectionLabel.RemoveFromClassList("status-ok");
            _connectionLabel.RemoveFromClassList("status-warn");
            _connectionLabel.AddToClassList(state == WsConnectionState.Connected ? "status-ok" : "status-warn");
        }

        private void HandleConnectionStateChanged(WsConnectionState state)
        {
            var previousState = _lastConnectionState;
            _lastConnectionState = state;

            RenderConnection(state);

            if (state == WsConnectionState.Reconnecting || state == WsConnectionState.Disconnected)
            {
                ClearTransientCommandState();
                SetCommandStatus("Connection lost. Cleared pending command UI.");
                return;
            }

            if (state == WsConnectionState.Connected && previousState != WsConnectionState.Connected)
                _ = RecoverAuthoritativeStateAsync();
        }

        private async Task ChangeStrataTaxAsync(string strata, float delta)
        {
            if (!EnsureCountrySelected())
                return;

            var fieldKey = GetStrataTaxFieldKey(strata);
            var desiredValue = Mathf.Clamp01(GetCurrentScheduledValue(fieldKey, GetCurrentStrataTax(strata)) + delta);
            await UpdateBudgetPreviewAsync("tax", strata, desiredValue);

            _commandScheduler.SetDesiredValue(
                fieldKey,
                desiredValue,
                value => _commandService.ChangeStrataTaxAsync(_controlledCountryId, strata, value));
        }

        private async Task ChangeFlatTaxAsync(float delta)
        {
            if (!EnsureCountrySelected())
                return;

            var desiredValue = Mathf.Clamp01(GetCurrentScheduledValue(FlatTaxFieldKey, GetCurrentFlatTax()) + delta);
            await UpdateBudgetPreviewAsync("tax", "flat", desiredValue);

            _commandScheduler.SetDesiredValue(
                FlatTaxFieldKey,
                desiredValue,
                value => _commandService.ChangeTaxRateAsync(_controlledCountryId, Mathf.RoundToInt(value * 100f)));
        }

        private async Task ChangeSpendingAsync(string category, float delta)
        {
            if (!EnsureCountrySelected())
                return;

            var fieldKey = GetSpendingFieldKey(category);
            var desiredValue = Mathf.Clamp01(GetCurrentScheduledValue(fieldKey, GetCurrentSpending(category)) + delta);
            await UpdateBudgetPreviewAsync("spending", category, desiredValue);

            _commandScheduler.SetDesiredValue(
                fieldKey,
                desiredValue,
                value => _commandService.ChangeSpendingAsync(_controlledCountryId, category, value));
        }

        private async Task QueueBuildingAsync(string buildingType)
        {
            if (_selectedProvince == null)
                return;

            var provinceId = _selectedProvince.id;
            var provinceName = _selectedProvince.name;
            var commandKey = GetBuildCommandKey(provinceId, buildingType);
            if (_pendingBuildCommandKeys.Contains(commandKey))
                return;

            try
            {
                SetBuildCommandPending(provinceId, buildingType, true);
                _selectedProvinceBuildStatus = $"Pending: {buildingType} in {provinceName}";
                await RenderConstructionAsync(provinceId);

                var response = await _commandService.QueueBuildingAsync(provinceId, buildingType);
                var outcome = _commandService.ToOutcome(response);
                SetCommandStatus(CommandOutcomeMapper.Format(outcome));
                _selectedProvinceBuildStatus = FormatBuildOutcome(buildingType, outcome);
                await RenderConstructionAsync(provinceId);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[WorldUI] Build command failed: {ex.Message}");
                SetCommandStatus($"Build command failed: {ex.Message}");
                _selectedProvinceBuildStatus = $"Last command: {buildingType} failed: {ex.Message}";
                await RenderConstructionAsync(provinceId);
            }
            finally
            {
                SetBuildCommandPending(provinceId, buildingType, false);
            }
        }

        private async Task MoveSelectedArmyAsync()
        {
            if (_selectedArmy == null)
            {
                SetCommandStatus("Select an army first.");
                return;
            }

            if (_selectedProvince == null)
            {
                SetCommandStatus("Select a destination province first.");
                return;
            }

            try
            {
                SetCommandStatus($"Moving {ShortId(_selectedArmy.id)} to {_selectedProvince.name}...");
                var response = await _commandService.MoveArmyAsync(_selectedArmy.id, _selectedProvince.id);
                SetCommandStatus(CommandOutcomeMapper.Format(_commandService.ToOutcome(response)));
                await RefreshMilitaryAsync();
            }
            catch (Exception ex)
            {
                Debug.LogError($"[WorldUI] Move army failed: {ex.Message}");
                SetCommandStatus($"Move army failed: {ex.Message}");
            }
        }

        private async Task DeclareWarFromSelectionAsync()
        {
            var targetCountryId = GetSelectedForeignCountryId();
            if (string.IsNullOrEmpty(targetCountryId))
            {
                SetCommandStatus("Select a foreign province first.");
                return;
            }

            await SubmitDiplomacyCommandAsync("DeclareWar", targetCountryId);
        }

        private async Task MakePeaceFromSelectionAsync()
        {
            var targetCountryId = GetSelectedForeignCountryId() ?? GetFirstActiveWarTargetId();
            if (string.IsNullOrEmpty(targetCountryId))
            {
                SetCommandStatus("Select an enemy province or use an active war.");
                return;
            }

            await SubmitDiplomacyCommandAsync("MakePeace", targetCountryId);
        }

        private async Task SubmitDiplomacyCommandAsync(string commandType, string targetCountryId)
        {
            try
            {
                var targetName = GetCountryName(targetCountryId);
                SetCommandStatus($"{commandType} {targetName}...");
                var response = commandType == "MakePeace"
                    ? await _commandService.MakePeaceAsync(targetCountryId)
                    : await _commandService.DeclareWarAsync(targetCountryId);
                SetCommandStatus(CommandOutcomeMapper.Format(_commandService.ToOutcome(response)));
                await RefreshMilitaryAsync();
                await RefreshEventFeedAsync();
            }
            catch (Exception ex)
            {
                Debug.LogError($"[WorldUI] {commandType} failed: {ex.Message}");
                SetCommandStatus($"{commandType} failed: {ex.Message}");
            }
        }

        private async Task RecoverAuthoritativeStateAsync()
        {
            ClearTransientCommandState();
            SetCommandStatus("Connection restored. Refreshing authoritative state...");

            try
            {
                await RefreshAllAsync();
            }
            catch (Exception ex)
            {
                Debug.LogError($"[WorldUI] Reconnect recovery failed: {ex.Message}");
                SetCommandStatus($"Reconnect refresh failed: {ex.Message}");
            }
        }

        private bool EnsureCountrySelected()
        {
            if (string.IsNullOrEmpty(_controlledCountryId))
                _controlledCountryId = PlayerSession.ControlledCountryId;
            return !string.IsNullOrEmpty(_controlledCountryId);
        }

        private void ToggleSort(string sort)
        {
            if (_sort == sort)
                _order = _order == "asc" ? "desc" : "asc";
            else
            {
                _sort = sort;
                _order = "asc";
            }
        }

        private float GetCurrentStrataTax(string strata)
        {
            if (_lastInspector == null)
                return 0.10f;

            var flat = _lastInspector.tax_rate / 100f;
            var value = strata switch
            {
                "middle" => _lastInspector.middle_tax_rate,
                "rich" => _lastInspector.rich_tax_rate,
                _ => _lastInspector.poor_tax_rate
            };
            return value < 0f ? flat : NormalizeRate(value);
        }

        private float GetCurrentFlatTax()
        {
            if (_lastInspector == null)
                return 0.10f;

            return Mathf.Clamp01(_lastInspector.tax_rate / 100f);
        }

        private float GetCurrentSpending(string category)
        {
            if (_lastInspector == null)
                return 0.50f;

            return category switch
            {
                "military" => NormalizeRate(_lastInspector.military_spending),
                "administration" => NormalizeRate(_lastInspector.administration_spending),
                _ => NormalizeRate(_lastInspector.education_spending)
            };
        }

        private static float NormalizeRate(float value) => value > 1f ? Mathf.Clamp01(value / 100f) : Mathf.Clamp01(value);

        private static string FormatStrataTax(float strataRate, int flatRate)
        {
            if (strataRate < 0f) return $"{flatRate}%";
            return strataRate <= 1f ? $"{strataRate * 100f:F0}%" : $"{strataRate:F0}%";
        }

        private void HandleScheduledCommandStatusChanged(ScheduledCommandStatus status)
        {
            if (status == null)
                return;

            if (status.State == ScheduledCommandState.Idle)
            {
                _scheduledValues.Remove(status.FieldKey);
            }
            else
            {
                _scheduledValues[status.FieldKey] = status.DesiredValue;
            }

            switch (status.State)
            {
                case ScheduledCommandState.Drafting:
                case ScheduledCommandState.Debouncing:
                    SetCommandStatus($"Queued {FormatScheduledField(status.FieldKey)}: {status.DesiredValue:P0}");
                    RenderScheduledFieldState(status.FieldKey, $"Draft {status.DesiredValue:P0}", "budget-state-draft", pending: false, retry: false);
                    break;
                case ScheduledCommandState.Submitting:
                    SetCommandStatus($"Submitting {FormatScheduledField(status.FieldKey)}: {status.LastSubmittedValue:P0}");
                    RenderScheduledFieldState(status.FieldKey, $"Pending {status.LastSubmittedValue:P0}", "budget-state-submitting", pending: true, retry: false);
                    break;
                case ScheduledCommandState.RetryScheduled:
                    SetCommandStatus(CommandOutcomeMapper.Format(_commandService.ToOutcome(status.LastResponse)));
                    RenderScheduledFieldState(status.FieldKey, FormatRetryLabel(status), "budget-state-retry", pending: false, retry: true);
                    break;
                case ScheduledCommandState.Settled:
                    SetCommandStatus(CommandOutcomeMapper.Format(_commandService.ToOutcome(status.LastResponse)));
                    RenderScheduledFieldState(status.FieldKey, FormatSettledLabel(status), "budget-state-settled", pending: false, retry: false);
                    _ = RefreshCountryInspectorAsync();
                    break;
                case ScheduledCommandState.Idle:
                    RenderScheduledFieldState(status.FieldKey, "Authoritative", "budget-state-idle", pending: false, retry: false);
                    break;
            }
        }

        private float GetCurrentScheduledValue(string fieldKey, float fallbackValue)
        {
            return _scheduledValues.TryGetValue(fieldKey, out var value) ? value : fallbackValue;
        }

        private static string GetBuildCommandKey(string provinceId, string buildingType) => $"{provinceId}:{buildingType}";

        private const string FlatTaxFieldKey = "tax:flat";

        private static string GetStrataTaxFieldKey(string strata) => $"tax:{strata}";

        private static string GetSpendingFieldKey(string category) => $"spending:{category}";

        private static string FormatScheduledField(string fieldKey)
        {
            if (fieldKey.StartsWith("tax:"))
                return fieldKey == FlatTaxFieldKey ? "flat tax" : $"{fieldKey.Substring("tax:".Length)} tax";
            if (fieldKey.StartsWith("spending:"))
                return $"{fieldKey.Substring("spending:".Length)} spending";
            return fieldKey;
        }

        private void ResetBudgetFieldStateRows()
        {
            foreach (var fieldKey in _scheduledStateLabels.Keys)
                RenderScheduledFieldState(fieldKey, "Authoritative", "budget-state-idle", pending: false, retry: false);
        }

        private void RefreshIdleBudgetFieldStateRows(AdminCountryInspectorData inspector)
        {
            if (inspector == null)
                return;

            UpdateIdleFieldState(FlatTaxFieldKey, "Flat tax", GetCurrentFlatTax());
            UpdateIdleFieldState(GetStrataTaxFieldKey("poor"), "Poor tax", GetCurrentStrataTax("poor"));
            UpdateIdleFieldState(GetStrataTaxFieldKey("middle"), "Middle tax", GetCurrentStrataTax("middle"));
            UpdateIdleFieldState(GetStrataTaxFieldKey("rich"), "Rich tax", GetCurrentStrataTax("rich"));
            UpdateIdleFieldState(GetSpendingFieldKey("education"), "Education spending", GetCurrentSpending("education"));
            UpdateIdleFieldState(GetSpendingFieldKey("military"), "Military spending", GetCurrentSpending("military"));
            UpdateIdleFieldState(GetSpendingFieldKey("administration"), "Administration spending", GetCurrentSpending("administration"));
        }

        private void UpdateIdleFieldState(string fieldKey, string displayName, float value)
        {
            if (_scheduledValues.ContainsKey(fieldKey))
                return;

            RenderScheduledFieldState(fieldKey, $"{value:P0}", "budget-state-idle", pending: false, retry: false);
        }

        private void RenderScheduledFieldState(string fieldKey, string stateText, string stateClass, bool pending, bool retry)
        {
            if (_scheduledStateLabels.TryGetValue(fieldKey, out var label))
            {
                label.text = $"{FormatScheduledFieldLabel(fieldKey)}: {stateText}";
                label.RemoveFromClassList("budget-state-idle");
                label.RemoveFromClassList("budget-state-draft");
                label.RemoveFromClassList("budget-state-submitting");
                label.RemoveFromClassList("budget-state-retry");
                label.RemoveFromClassList("budget-state-settled");
                label.AddToClassList(stateClass);
            }

            if (_scheduledButtons.TryGetValue(fieldKey, out var buttons))
            {
                foreach (var button in buttons)
                {
                    button.RemoveFromClassList("button-pending");
                    button.RemoveFromClassList("button-retry");
                    if (pending)
                        button.AddToClassList("button-pending");
                    if (retry)
                        button.AddToClassList("button-retry");
                }
            }
        }

        private static string FormatScheduledFieldLabel(string fieldKey)
        {
            if (fieldKey == FlatTaxFieldKey) return "Flat tax";
            if (fieldKey == GetStrataTaxFieldKey("poor")) return "Poor tax";
            if (fieldKey == GetStrataTaxFieldKey("middle")) return "Middle tax";
            if (fieldKey == GetStrataTaxFieldKey("rich")) return "Rich tax";
            if (fieldKey == GetSpendingFieldKey("education")) return "Education spending";
            if (fieldKey == GetSpendingFieldKey("military")) return "Military spending";
            if (fieldKey == GetSpendingFieldKey("administration")) return "Administration spending";
            return FormatScheduledField(fieldKey);
        }

        private static string FormatRetryLabel(ScheduledCommandStatus status)
        {
            var retryTicks = status.LastResponse?.retryAfterTicks ?? 0;
            return retryTicks > 0 ? $"Retry in {retryTicks} tick(s)" : "Retry scheduled";
        }

        private static string FormatSettledLabel(ScheduledCommandStatus status)
        {
            if (status.LastResponse != null && status.LastResponse.status == "rejected" && !string.IsNullOrEmpty(status.LastResponse.message))
                return status.LastResponse.message;

            return $"Settled {status.LastSubmittedValue:P0}";
        }

        private async Task RefreshBudgetPreviewBaselineAsync()
        {
            if (_budgetEffectsLabel == null || _lastInspector == null || string.IsNullOrEmpty(_controlledCountryId))
                return;

            try
            {
                var preview = await _apiClient.GetBudgetAdjustmentPreviewAsync(
                    _controlledCountryId,
                    "spending",
                    "education",
                    GetCurrentSpending("education"));
                RenderBudgetPreview(preview);
            }
            catch
            {
                _budgetEffectsLabel.text = string.Empty;
            }
        }

        private async Task UpdateBudgetPreviewAsync(string kind, string target, float proposedValue)
        {
            if (_budgetEffectsLabel == null || string.IsNullOrEmpty(_controlledCountryId))
                return;

            try
            {
                var preview = await _apiClient.GetBudgetAdjustmentPreviewAsync(_controlledCountryId, kind, target, proposedValue);
                RenderBudgetPreview(preview);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[WorldUI] Budget preview failed: {ex.Message}");
                _budgetEffectsLabel.text = "Budget preview unavailable.";
            }
        }

        private void RenderBudgetPreview(BudgetAdjustmentPreviewData preview)
        {
            if (_budgetEffectsLabel == null)
                return;

            if (preview == null)
            {
                _budgetEffectsLabel.text = string.Empty;
                return;
            }

            var effects = preview.effects != null && preview.effects.Count > 0
                ? " " + string.Join(" ", preview.effects)
                : string.Empty;
            _budgetEffectsLabel.text = string.IsNullOrWhiteSpace(preview.summary)
                ? effects.Trim()
                : $"{preview.summary}.{effects}";
        }

        private async Task RefreshConstructionOptionsAsync(string provinceId)
        {
            if (_constructionOptionsLabel == null)
                return;

            try
            {
                var options = await _apiClient.GetConstructionOptionsAsync(provinceId);
                _serverUnavailableBuildTypes.Clear();
                if (options != null)
                {
                    foreach (var option in options.Where(option => option != null && !option.available && !string.IsNullOrEmpty(option.building_type)))
                        _serverUnavailableBuildTypes.Add(option.building_type);
                }

                RenderConstructionOptions(options);
                RefreshBuildButtons();
            }
            catch (Exception ex)
            {
                Debug.LogError($"[WorldUI] Construction options failed: {ex.Message}");
                _constructionOptionsLabel.text = "Construction options unavailable.";
            }
        }

        private void RenderConstructionOptions(List<ConstructionOptionPreviewData> options)
        {
            if (_constructionOptionsLabel == null)
                return;

            if (options == null || options.Count == 0)
            {
                _constructionOptionsLabel.text = string.Empty;
                return;
            }

            _constructionOptionsLabel.text = string.Join(
                "\n",
                options.Select(option =>
                    $"{CapitalizeLabel(option.building_type)}: {(option.available ? "available" : option.message)}"));
        }

        private void ClearTransientCommandState()
        {
            _commandScheduler?.Clear();
            _scheduledValues.Clear();
            _pendingBuildCommandKeys.Clear();
            _serverUnavailableBuildTypes.Clear();
            _selectedProvinceBuildStatus = null;
            _selectedArmy = null;
            ResetBudgetFieldStateRows();
            RefreshBuildButtons();
            RenderMilitary();
            RefreshMilitaryActionState();

            if (_selectedProvince != null && _constructionLabel != null)
                _constructionLabel.text = "Construction: refreshing...";
        }

        private void SetBuildCommandPending(string provinceId, string buildingType, bool isPending)
        {
            var commandKey = GetBuildCommandKey(provinceId, buildingType);
            if (isPending)
                _pendingBuildCommandKeys.Add(commandKey);
            else
                _pendingBuildCommandKeys.Remove(commandKey);

            if (_selectedProvince != null && _selectedProvince.id == provinceId)
                RefreshBuildButtons();
        }

        private void RefreshBuildButtons()
        {
            foreach (var kv in _buildButtons)
            {
                var buildingType = kv.Key;
                var button = kv.Value;
                var defaultText = _buildButtonDefaultTexts.TryGetValue(buildingType, out var text) ? text : buildingType;
                var isPending = _selectedProvince != null && _pendingBuildCommandKeys.Contains(GetBuildCommandKey(_selectedProvince.id, buildingType));
                var serverUnavailable = _selectedProvince != null && _serverUnavailableBuildTypes.Contains(buildingType);

                button.text = isPending ? $"{defaultText}..." : defaultText;
                button.SetEnabled(!isPending && !serverUnavailable);
                button.EnableInClassList("button-pending", isPending);
            }
        }

        private static string FormatBuildOutcome(string buildingType, CommandOutcomeData outcome)
        {
            if (outcome == null)
                return $"Last command: {buildingType} unknown";

            return outcome.Kind switch
            {
                CommandOutcomeKind.Accepted => $"Last command: {buildingType} accepted",
                CommandOutcomeKind.Applied => $"Last command: {buildingType} applied",
                CommandOutcomeKind.Rejected => $"Last command: {buildingType} rejected" +
                    (outcome.RetryAfterTicks > 0 ? $" (retry in {outcome.RetryAfterTicks} tick(s))" : string.Empty),
                CommandOutcomeKind.Failed => $"Last command: {buildingType} failed",
                _ => $"Last command: {buildingType} {(string.IsNullOrEmpty(outcome.RawStatus) ? "unknown" : outcome.RawStatus)}"
            };
        }

        private static string CapitalizeLabel(string value) =>
            string.IsNullOrWhiteSpace(value) ? value : char.ToUpperInvariant(value[0]) + value.Substring(1);

        private static string FormatDictionary(Dictionary<string, float> values)
        {
            if (values == null || values.Count == 0)
                return string.Empty;

            return string.Join(", ", values.OrderBy(kv => kv.Key).Select(kv => $"{kv.Key} {kv.Value:F2}"));
        }

        private string GetSelectedForeignCountryId()
        {
            if (_selectedProvince == null || string.IsNullOrEmpty(_selectedProvince.owner_id))
                return null;
            return _selectedProvince.owner_id == _controlledCountryId ? null : _selectedProvince.owner_id;
        }

        private string GetFirstActiveWarTargetId()
        {
            var war = (_lastWars ?? new List<WarData>()).FirstOrDefault(war => war != null && war.is_active && IsWarRelevant(war));
            if (war == null)
                return null;
            return war.attacker_country_id == _controlledCountryId ? war.defender_country_id : war.attacker_country_id;
        }

        private bool IsWarRelevant(WarData war) =>
            war.attacker_country_id == _controlledCountryId || war.defender_country_id == _controlledCountryId;

        private string GetCountryName(string countryId) =>
            !string.IsNullOrEmpty(countryId) && _knownCountriesById.TryGetValue(countryId, out var country)
                ? country.name
                : countryId;

        private static string ShortId(string id) =>
            string.IsNullOrEmpty(id) || id.Length <= 8 ? id : id.Substring(0, 8);

        private void RefreshMilitaryActionState()
        {
            if (_militarySelectionLabel == null)
                return;

            var selectedArmyText = _selectedArmy == null
                ? "Army: none"
                : $"Army: {ShortId(_selectedArmy.id)} at {_selectedArmy.location_province_name}";
            var selectedProvinceText = _selectedProvince == null
                ? "Target: none"
                : $"Target: {_selectedProvince.name} ({_selectedProvince.owner_name})";
            _militarySelectionLabel.text = $"{selectedArmyText}\n{selectedProvinceText}";

            var hasArmy = _selectedArmy != null;
            var hasProvince = _selectedProvince != null;
            var foreignTarget = GetSelectedForeignCountryId();
            var activeWarTarget = GetFirstActiveWarTargetId();

            _moveArmyButton?.SetEnabled(hasArmy && hasProvince);
            _declareWarButton?.SetEnabled(!string.IsNullOrEmpty(foreignTarget));
            _makePeaceButton?.SetEnabled(!string.IsNullOrEmpty(foreignTarget) || !string.IsNullOrEmpty(activeWarTarget));
        }

        private void SetCommandStatus(string text)
        {
            if (_commandStatusLabel != null)
                _commandStatusLabel.text = text ?? "";
        }

        private void BindDashboardTabs()
        {
            _tabButtons.Clear();
            foreach (var tab in DashboardTabs)
            {
                var button = _root.Q<Button>($"tab-{tab}");
                if (button == null) continue;
                _tabButtons[tab] = button;
                var capturedTab = tab;
                button.clicked += () => SetActiveTab(capturedTab);
            }
            _politicsSummaryLabel = _root.Q<Label>("politics-summary-label");
            _politicsDetailLabel = _root.Q<Label>("politics-detail-label");
            ApplyActiveTab();
        }

        private void SetActiveTab(string tab)
        {
            if (string.IsNullOrEmpty(tab) || _activeTab == tab) return;
            _activeTab = tab;
            ApplyActiveTab();
            if (tab == "politics" && _lastInspector != null)
                RenderPoliticsView(_lastInspector);
        }

        private void ApplyActiveTab()
        {
            foreach (var tab in DashboardTabs)
                _root.RemoveFromClassList($"view-{tab}");
            _root.AddToClassList($"view-{_activeTab}");

            foreach (var kv in _tabButtons)
            {
                if (kv.Key == _activeTab)
                    kv.Value.AddToClassList("tab-active");
                else
                    kv.Value.RemoveFromClassList("tab-active");
            }
        }

        private void RenderPoliticsView(AdminCountryInspectorData inspector)
        {
            if (_politicsSummaryLabel == null || _politicsDetailLabel == null) return;
            _politicsSummaryLabel.text = $"Reform pressure: {inspector.reform_pressure:F1} / 100";

            float weightedLife = 0f;
            long totalSize = 0;
            if (inspector.pop_groups != null)
            {
                foreach (var g in inspector.pop_groups)
                {
                    if (g == null || g.size <= 0) continue;
                    totalSize += g.size;
                    weightedLife += g.life_needs_fulfillment * g.size;
                }
            }
            var avgLife = totalSize > 0 ? weightedLife / totalSize : 0f;
            var militancyContribution = inspector.average_militancy * 6f;
            var consciousnessContribution = inspector.average_consciousness * 2f;
            var unemploymentContribution = inspector.unemployment_share * 20f;
            var unmetNeedsContribution = (1f - avgLife) * 12f;

            _politicsDetailLabel.text =
                $"Militancy term:        {militancyContribution:F2}   (avg militancy {inspector.average_militancy:F2} × 6)\n" +
                $"Consciousness term:    {consciousnessContribution:F2}   (avg consciousness {inspector.average_consciousness:F2} × 2)\n" +
                $"Unemployment term:     {unemploymentContribution:F2}   (unemployment {inspector.unemployment_share:P1} × 20)\n" +
                $"Unmet-needs term:      {unmetNeedsContribution:F2}   ((1 - avg life needs {avgLife:P0}) × 12)\n" +
                $"\nLiteracy {inspector.average_literacy:P0}, Population {inspector.population:N0}, " +
                $"Provinces {inspector.province_count}";
        }

        private void RenderReformPressure(float pressure)
        {
            if (_reformPressureLabel == null) return;
            _reformPressureLabel.text = $"{pressure:F1}";
            _reformPressureLabel.RemoveFromClassList("status-ok");
            _reformPressureLabel.RemoveFromClassList("status-warn");
            _reformPressureLabel.RemoveFromClassList("status-crit");
            if (pressure >= 25f)
                _reformPressureLabel.AddToClassList("status-crit");
            else if (pressure >= 10f)
                _reformPressureLabel.AddToClassList("status-warn");
            else
                _reformPressureLabel.AddToClassList("status-ok");
        }

        private void BindPopulationSlicer()
        {
            _popGroupByButtons.Clear();
            _popGroupByButtons["type"] = Button("pop-group-by-type-button");
            _popGroupByButtons["culture"] = Button("pop-group-by-culture-button");
            _popGroupByButtons["religion"] = Button("pop-group-by-religion-button");
            _popGroupByButtons["strata"] = Button("pop-group-by-strata-button");
            _popGroupByButtons["province"] = Button("pop-group-by-province-button");
            foreach (var kv in _popGroupByButtons)
            {
                var key = kv.Key;
                kv.Value.clicked += () => SetPopGroupBy(key);
            }
            ApplyPopGroupByActiveStyle();

            _popFilterTypeDropdown = Required<DropdownField>("pop-filter-type");
            _popFilterCultureDropdown = Required<DropdownField>("pop-filter-culture");
            _popFilterReligionDropdown = Required<DropdownField>("pop-filter-religion");
            _popFilterStrataDropdown = Required<DropdownField>("pop-filter-strata");

            _popFilterTypeDropdown.choices = new List<string> { PopFilterAll };
            _popFilterCultureDropdown.choices = new List<string> { PopFilterAll };
            _popFilterReligionDropdown.choices = new List<string> { PopFilterAll };
            _popFilterStrataDropdown.choices = new List<string> { PopFilterAll };
            _popFilterTypeDropdown.value = PopFilterAll;
            _popFilterCultureDropdown.value = PopFilterAll;
            _popFilterReligionDropdown.value = PopFilterAll;
            _popFilterStrataDropdown.value = PopFilterAll;

            _popFilterTypeDropdown.RegisterValueChangedCallback(evt => { _popFilterType = NormalizePopFilter(evt.newValue); RerenderPopulationFromCache(); });
            _popFilterCultureDropdown.RegisterValueChangedCallback(evt => { _popFilterCulture = NormalizePopFilter(evt.newValue); RerenderPopulationFromCache(); });
            _popFilterReligionDropdown.RegisterValueChangedCallback(evt => { _popFilterReligion = NormalizePopFilter(evt.newValue); RerenderPopulationFromCache(); });
            _popFilterStrataDropdown.RegisterValueChangedCallback(evt => { _popFilterStrata = NormalizePopFilter(evt.newValue); RerenderPopulationFromCache(); });

            Button("pop-filter-clear-button").clicked += () =>
            {
                _popFilterType = null;
                _popFilterCulture = null;
                _popFilterReligion = null;
                _popFilterStrata = null;
                if (_popFilterTypeDropdown != null) _popFilterTypeDropdown.value = PopFilterAll;
                if (_popFilterCultureDropdown != null) _popFilterCultureDropdown.value = PopFilterAll;
                if (_popFilterReligionDropdown != null) _popFilterReligionDropdown.value = PopFilterAll;
                if (_popFilterStrataDropdown != null) _popFilterStrataDropdown.value = PopFilterAll;
                RerenderPopulationFromCache();
            };
        }

        private static string NormalizePopFilter(string value) =>
            string.IsNullOrEmpty(value) || value == PopFilterAll ? null : value;

        private void SetPopGroupBy(string key)
        {
            if (_popGroupBy == key) return;
            _popGroupBy = key;
            ApplyPopGroupByActiveStyle();
            RerenderPopulationFromCache();
        }

        private void ApplyPopGroupByActiveStyle()
        {
            foreach (var kv in _popGroupByButtons)
            {
                if (kv.Key == _popGroupBy)
                    kv.Value.AddToClassList("pop-group-by-active");
                else
                    kv.Value.RemoveFromClassList("pop-group-by-active");
            }
        }

        private void RerenderPopulationFromCache()
        {
            if (_lastInspector == null || _popSummaryLabel == null) return;
            _popSummaryLabel.text = BuildPopulationSummaryText(_lastInspector);
        }

        private void RefreshPopFilterOptions(AdminCountryInspectorData inspector)
        {
            if (_popFilterTypeDropdown == null) return;

            var groups = inspector?.pop_groups ?? new List<AdminCountryPopGroupData>();
            UpdateDropdownChoices(_popFilterTypeDropdown, groups.Select(g => g.pop_type), ref _popFilterType);
            UpdateDropdownChoices(_popFilterCultureDropdown, groups.Select(g => g.culture), ref _popFilterCulture);
            UpdateDropdownChoices(_popFilterReligionDropdown, groups.Select(g => g.religion), ref _popFilterReligion);
            UpdateDropdownChoices(_popFilterStrataDropdown, groups.Select(g => g.strata), ref _popFilterStrata);
        }

        private static void UpdateDropdownChoices(DropdownField dropdown, IEnumerable<string> source, ref string currentValue)
        {
            var values = source
                .Where(v => !string.IsNullOrEmpty(v))
                .Distinct(StringComparer.Ordinal)
                .OrderBy(v => v, StringComparer.Ordinal)
                .ToList();
            var choices = new List<string> { PopFilterAll };
            choices.AddRange(values);
            dropdown.choices = choices;

            if (!string.IsNullOrEmpty(currentValue) && !values.Contains(currentValue, StringComparer.Ordinal))
                currentValue = null;
            dropdown.SetValueWithoutNotify(string.IsNullOrEmpty(currentValue) ? PopFilterAll : currentValue);
        }

        private string BuildPopulationSummaryText(AdminCountryInspectorData inspector)
        {
            var groups = inspector.pop_groups ?? new List<AdminCountryPopGroupData>();
            var filtered = groups.Where(g => g != null
                && (_popFilterType == null     || string.Equals(g.pop_type, _popFilterType,     StringComparison.Ordinal))
                && (_popFilterCulture == null  || string.Equals(g.culture,  _popFilterCulture,  StringComparison.Ordinal))
                && (_popFilterReligion == null || string.Equals(g.religion, _popFilterReligion, StringComparison.Ordinal))
                && (_popFilterStrata == null   || string.Equals(g.strata,   _popFilterStrata,   StringComparison.Ordinal)))
                .ToList();

            long totalSize = filtered.Sum(g => (long)g.size);
            long totalEmployed = filtered.Sum(g => (long)g.employed_count);
            long totalUnemployed = filtered.Sum(g => (long)g.unemployed_count);
            float weightedLiteracy = totalSize > 0
                ? (float)(filtered.Sum(g => (double)g.literacy * g.size) / totalSize)
                : 0f;
            float weightedMilitancy = totalSize > 0
                ? (float)(filtered.Sum(g => (double)g.militancy * g.size) / totalSize)
                : 0f;
            float weightedLife = totalSize > 0
                ? (float)(filtered.Sum(g => (double)g.life_needs_fulfillment * g.size) / totalSize)
                : 0f;
            float unemploymentRate = (totalEmployed + totalUnemployed) > 0
                ? (float)totalUnemployed / (totalEmployed + totalUnemployed)
                : 0f;

            var activeFilters = new List<string>();
            if (_popFilterType != null)     activeFilters.Add($"type={_popFilterType}");
            if (_popFilterCulture != null)  activeFilters.Add($"culture={_popFilterCulture}");
            if (_popFilterReligion != null) activeFilters.Add($"religion={_popFilterReligion}");
            if (_popFilterStrata != null)   activeFilters.Add($"strata={_popFilterStrata}");
            var filterSuffix = activeFilters.Count > 0 ? $"   filters: {string.Join(", ", activeFilters)}" : "";

            var text =
                $"National POPs: {inspector.population:N0}   Selection: {totalSize:N0} ({(inspector.population > 0 ? (float)totalSize / inspector.population : 0f):P0}){filterSuffix}\n" +
                $"Literacy {weightedLiteracy:P0}  Militancy {weightedMilitancy:F2}  Life-needs {weightedLife:P0}  Unemployment {unemploymentRate:P1}";

            if (filtered.Count == 0)
            {
                text += "\n\n(no POPs match)";
                return text;
            }

            text += $"\n\nGrouped by {_popGroupBy.ToUpperInvariant()}:";
            var rows = filtered
                .GroupBy(g => GetPopGroupKey(g, _popGroupBy), StringComparer.Ordinal)
                .Select(grp =>
                {
                    long size = grp.Sum(g => (long)g.size);
                    long emp = grp.Sum(g => (long)g.employed_count);
                    long unemp = grp.Sum(g => (long)g.unemployed_count);
                    float lit = size > 0 ? (float)(grp.Sum(g => (double)g.literacy * g.size) / size) : 0f;
                    float mil = size > 0 ? (float)(grp.Sum(g => (double)g.militancy * g.size) / size) : 0f;
                    float life = size > 0 ? (float)(grp.Sum(g => (double)g.life_needs_fulfillment * g.size) / size) : 0f;
                    float unempShare = (emp + unemp) > 0 ? (float)unemp / (emp + unemp) : 0f;
                    float share = totalSize > 0 ? (float)size / totalSize : 0f;
                    return new
                    {
                        Key = grp.Key,
                        Size = size,
                        Share = share,
                        Lit = lit,
                        Mil = mil,
                        Life = life,
                        UnempShare = unempShare
                    };
                })
                .OrderByDescending(r => r.Size)
                .ToList();

            foreach (var row in rows)
            {
                text +=
                    $"\n  {row.Key}: {row.Size:N0} ({row.Share:P0})" +
                    $"  unemp {row.UnempShare:P0}  lit {row.Lit:P0}  mil {row.Mil:F2}  life {row.Life:P0}";
            }

            return text;
        }

        private static string GetPopGroupKey(AdminCountryPopGroupData g, string groupBy) => groupBy switch
        {
            "culture"  => string.IsNullOrEmpty(g.culture)  ? "(unknown)" : g.culture,
            "religion" => string.IsNullOrEmpty(g.religion) ? "(unknown)" : g.religion,
            "strata"   => string.IsNullOrEmpty(g.strata)   ? "(unknown)" : g.strata,
            "province" => string.IsNullOrEmpty(g.province_name) ? "(unknown)" : g.province_name,
            _          => string.IsNullOrEmpty(g.pop_type) ? "(unknown)" : $"{g.pop_type} ({g.strata})",
        };

        private T Required<T>(string name) where T : VisualElement
        {
            var element = _root.Q<T>(name);
            if (element == null)
                throw new InvalidOperationException($"UI Toolkit element '{name}' was not found.");
            return element;
        }

        private Button Button(string name) => Required<Button>(name);
    }
}
