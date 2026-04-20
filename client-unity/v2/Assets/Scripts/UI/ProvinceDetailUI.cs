using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using VictoriaLike.Client.Api;

namespace VictoriaLike.Client.UI
{
    public class ProvinceDetailUI : MonoBehaviour
    {
        [SerializeField] private Text provinceName;
        [SerializeField] private Text ownerInfo;
        [SerializeField] private Text marketInfo;
        [SerializeField] private Text populationInfo;
        [SerializeField] private Text marketGoodsInfo;
        [SerializeField] private Text rgoInfo;
        [SerializeField] private Text popGroupsInfo;
        [SerializeField] private Text factoriesInfo;
        [SerializeField] private Text constructionInfo;
        [SerializeField] private Button buildFarmButton;
        [SerializeField] private Button buildMineButton;
        [SerializeField] private Button buildWorkshopButton;
        [SerializeField] private Button backButton;
        [SerializeField] private CanvasGroup canvasGroup;

        private IWorldApiClient _apiClient;
        private IWorldCommandService _commandService;
        private List<CountryData> _countries;
        private ProvinceData _currentProvince;
        private readonly HashSet<string> _pendingBuildCommandKeys = new();
        private readonly Dictionary<string, Button> _buildButtons = new();
        private readonly Dictionary<string, string> _buildButtonDefaultTexts = new();
        private string _buildStatusMessage;
        public event Action OnBackClicked;

        private void Start()
        {
            _apiClient = new WorldApiClient("http://localhost:5001");
            _commandService = new WorldCommandService(_apiClient, null);
            _countries = new List<CountryData>();

            if (backButton != null)
            {
                backButton.onClick.AddListener(() => GoBack());
            }
            if (buildFarmButton != null)
            {
                _buildButtons["farm"] = buildFarmButton;
                _buildButtonDefaultTexts["farm"] = buildFarmButton.GetComponentInChildren<Text>()?.text ?? "Farm";
                buildFarmButton.onClick.AddListener(() => _ = QueueBuildingAsync("farm"));
            }
            if (buildMineButton != null)
            {
                _buildButtons["mine"] = buildMineButton;
                _buildButtonDefaultTexts["mine"] = buildMineButton.GetComponentInChildren<Text>()?.text ?? "Mine";
                buildMineButton.onClick.AddListener(() => _ = QueueBuildingAsync("mine"));
            }
            if (buildWorkshopButton != null)
            {
                _buildButtons["workshop"] = buildWorkshopButton;
                _buildButtonDefaultTexts["workshop"] = buildWorkshopButton.GetComponentInChildren<Text>()?.text ?? "Workshop";
                buildWorkshopButton.onClick.AddListener(() => _ = QueueBuildingAsync("workshop"));
            }

            _ = LoadCountriesAsync();

            // Start hidden
            if (canvasGroup != null)
                canvasGroup.alpha = 0;
        }

        public async Task ShowProvinceDetailAsync(ProvinceData province)
        {
            try
            {
                _currentProvince = province;
                _buildStatusMessage = null;
                RefreshBuildButtons();

                // Show loading state
                if (canvasGroup != null)
                    canvasGroup.alpha = 1;

                // Fetch detailed province data
                var detail = await _apiClient.GetProvinceDetailAsync(province.id);

                // Display basic info
                if (provinceName != null)
                    provinceName.text = detail.name;

                // Find owner country for tax rate
                var ownerCountry = _countries.Find(c => c.id == detail.owner_id);
                if (ownerInfo != null)
                {
                    var taxInfo = ownerCountry != null ? $" (Tax: {ownerCountry.tax_rate}%)" : "";
                    ownerInfo.text = $"Owner: {detail.owner_name}{taxInfo}";
                }

                // Display market info
                if (marketInfo != null)
                    marketInfo.text = $"Market ID: {detail.market_id}";

                // Display population
                if (populationInfo != null)
                    populationInfo.text = $"Population: {detail.population:N0}";

                // Display market goods if available
                if (marketGoodsInfo != null)
                {
                    if (detail.market_goods != null && detail.market_goods.Count > 0)
                    {
                        var goodsList = "Market Goods:\n";
                        foreach (var good in detail.market_goods)
                        {
                            goodsList += $"  {good.Key}: {good.Value:F2}\n";
                        }
                        marketGoodsInfo.text = goodsList;
                    }
                    else
                    {
                        marketGoodsInfo.text = "No market goods data";
                    }
                }

                await PopulateInspectorAsync(detail.id);
                await PopulateConstructionAsync(detail.id);

                Debug.Log($"Showing detail for province: {detail.name}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error loading province detail: {ex.Message}");
                if (provinceName != null)
                    provinceName.text = $"Error: {ex.Message}";
            }
        }

        private async Task PopulateInspectorAsync(string provinceId)
        {
            try
            {
                var inspector = await _apiClient.GetProvinceInspectorAsync(provinceId);

                if (rgoInfo != null)
                {
                    rgoInfo.text = $"RGO: {inspector.rgo_type}\nWorkforce: {inspector.workforce:N0}\nNeeds: {inspector.needs_fulfillment:P0}";
                }

                if (popGroupsInfo != null)
                {
                    if (inspector.pop_groups != null && inspector.pop_groups.Count > 0)
                    {
                        var sb = new System.Text.StringBuilder("POPs:\n");
                        foreach (var pop in inspector.pop_groups)
                        {
                            sb.AppendLine($"  {pop.pop_type} ({pop.strata}): {pop.size:N0}  emp {pop.employed_count:N0}/unem {pop.unemployed_count:N0}  mil {pop.militancy:F1}  lit {pop.literacy:P0}");
                        }
                        popGroupsInfo.text = sb.ToString();
                    }
                    else
                    {
                        popGroupsInfo.text = "No POP groups";
                    }
                }

                if (factoriesInfo != null)
                {
                    if (inspector.factories != null && inspector.factories.Count > 0)
                    {
                        var sb = new System.Text.StringBuilder("Factories:\n");
                        foreach (var f in inspector.factories)
                        {
                            sb.AppendLine($"  {f.type} L{f.level} -> {f.output_good} {f.output_per_tick:F2}/t  craftsmen {f.employed_craftsmen:N0}/clerks {f.employed_clerks:N0}  profit {f.profit_last_tick:F1}");
                        }
                        factoriesInfo.text = sb.ToString();
                    }
                    else
                    {
                        factoriesInfo.text = "No factories";
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error loading province inspector: {ex.Message}");
            }
        }

        private async Task PopulateConstructionAsync(string provinceId)
        {
            try
            {
                if (constructionInfo == null)
                    return;

                var queue = await _apiClient.GetBuildingQueueAsync();
                var matching = queue.FindAll(item => item.province_id == provinceId);
                if (matching.Count == 0 && string.IsNullOrEmpty(_buildStatusMessage))
                {
                    constructionInfo.text = "Construction: none";
                    return;
                }

                var sb = new System.Text.StringBuilder("Construction:\n");
                if (!string.IsNullOrEmpty(_buildStatusMessage))
                    sb.AppendLine($"  {_buildStatusMessage}");

                foreach (var item in matching)
                {
                    sb.AppendLine($"  {item.building_type}: {item.ticks_remaining} ticks");
                }
                constructionInfo.text = sb.ToString();
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error loading construction queue: {ex.Message}");
                if (constructionInfo != null)
                    constructionInfo.text = $"Construction error: {ex.Message}";
            }
        }

        private async Task QueueBuildingAsync(string buildingType)
        {
            if (_currentProvince == null)
                return;

            var provinceId = _currentProvince.id;
            var commandKey = GetBuildCommandKey(provinceId, buildingType);
            if (_pendingBuildCommandKeys.Contains(commandKey))
                return;

            try
            {
                SetBuildCommandPending(provinceId, buildingType, true);
                _buildStatusMessage = $"Pending: {buildingType}";
                await PopulateConstructionAsync(provinceId);

                var response = await _commandService.QueueBuildingAsync(provinceId, buildingType);
                var outcome = _commandService.ToOutcome(response);
                Debug.Log($"Queued {buildingType}: {response.status} {response.message}");
                _buildStatusMessage = FormatBuildOutcome(buildingType, outcome);
                await PopulateConstructionAsync(provinceId);
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error queuing {buildingType}: {ex.Message}");
                _buildStatusMessage = $"Last command: {buildingType} failed: {ex.Message}";
                if (constructionInfo != null)
                    await PopulateConstructionAsync(provinceId);
            }
            finally
            {
                SetBuildCommandPending(provinceId, buildingType, false);
            }
        }

        private async Task LoadCountriesAsync()
        {
            try
            {
                _countries = await _apiClient.ListCountriesAsync();
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error loading countries: {ex.Message}");
            }
        }

        private void GoBack()
        {
            if (canvasGroup != null)
                canvasGroup.alpha = 0;
            OnBackClicked?.Invoke();
        }

        public void Hide()
        {
            if (canvasGroup != null)
                canvasGroup.alpha = 0;
        }

        private static string GetBuildCommandKey(string provinceId, string buildingType) => $"{provinceId}:{buildingType}";

        private void SetBuildCommandPending(string provinceId, string buildingType, bool isPending)
        {
            var commandKey = GetBuildCommandKey(provinceId, buildingType);
            if (isPending)
                _pendingBuildCommandKeys.Add(commandKey);
            else
                _pendingBuildCommandKeys.Remove(commandKey);

            if (_currentProvince != null && _currentProvince.id == provinceId)
                RefreshBuildButtons();
        }

        private void RefreshBuildButtons()
        {
            foreach (var kv in _buildButtons)
            {
                var buildingType = kv.Key;
                var button = kv.Value;
                var isPending = _currentProvince != null && _pendingBuildCommandKeys.Contains(GetBuildCommandKey(_currentProvince.id, buildingType));
                var label = button.GetComponentInChildren<Text>();
                if (label != null && _buildButtonDefaultTexts.TryGetValue(buildingType, out var defaultText))
                    label.text = isPending ? $"{defaultText}..." : defaultText;
                button.interactable = !isPending;
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
    }
}
