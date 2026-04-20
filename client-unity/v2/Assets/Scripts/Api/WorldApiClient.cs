using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace VictoriaLike.Client.Api
{
    [System.Serializable]
    public class WorldSummaryData
    {
        public long tick;
        public string world_date;
        public int country_count;
        public int province_count;
        public int market_count;
    }

    [System.Serializable]
    public class CountryData
    {
        public string id;
        public string name;
        public string tag;
        public int tax_rate;
        public float treasury;
        public int province_count;
        public string controller_actor_id;
        public string controller_username;
    }

    [System.Serializable]
    public class ProvinceData
    {
        public string id;
        public string name;
        public string owner_id;
        public string owner_name;
        public string market_id;
        public int population;
        public string rgo_type;
    }

    [System.Serializable]
    public class ProvinceDetailData : ProvinceData
    {
        public string market_name;
        public Dictionary<string, float> market_goods;
        public Dictionary<string, float> outputs_per_tick;
        public float needs_fulfillment;
    }

    [System.Serializable]
    public class BuildingQueueItemData
    {
        public string id;
        public string province_id;
        public string province_name;
        public string country_id;
        public string building_type;
        public int ticks_remaining;
        public string queued_at;
    }

    [System.Serializable]
    public class ConstructionOptionPreviewData
    {
        public string building_type;
        public bool available;
        public string rejection_reason;
        public string message;
        public float cost;
        public int build_ticks;
        public float treasury_after_command;
        public Dictionary<string, float> output_per_tick;
    }

    [System.Serializable]
    public class AdminCountryPopTypeData
    {
        public string pop_type;
        public string strata;
        public int size;
        public int employed;
        public int unemployed;
        public float average_literacy;
        public float average_militancy;
        public float average_consciousness;
        public float average_life_needs;
    }

    [System.Serializable]
    public class AdminCountryInspectorData
    {
        public string country_id;
        public string name;
        public string tag;
        public float treasury;
        public int tax_rate;
        public int province_count;
        public int population;
        public float poor_tax_rate;
        public float middle_tax_rate;
        public float rich_tax_rate;
        public float education_spending;
        public float military_spending;
        public float administration_spending;
        public float average_literacy;
        public float average_militancy;
        public float average_consciousness;
        public float unemployment_share;
        public float reform_pressure;
        public List<AdminCountryPopTypeData> pop_type_breakdown;
        public List<AdminCountryPopGroupData> pop_groups;
        public List<MarketWarningData> market_warnings;
    }

    [System.Serializable]
    public class AdminCountryPopGroupData
    {
        public string id;
        public string province_id;
        public string province_name;
        public string pop_type;
        public string strata;
        public string culture;
        public string religion;
        public int size;
        public int employed_count;
        public int unemployed_count;
        public float literacy;
        public float militancy;
        public float life_needs_fulfillment;
    }

    [System.Serializable]
    public class MarketWarningData
    {
        public string good_id;
        public string severity;
        public float price;
        public float supply;
        public float demand;
        public float fulfillment_rate;
        public string message;
    }

    [System.Serializable]
    public class BudgetAdjustmentPreviewData
    {
        public string country_id;
        public string kind;
        public string target;
        public float current_value;
        public float proposed_value;
        public float estimated_weekly_spending_cost_current;
        public float estimated_weekly_spending_cost_proposed;
        public float estimated_weekly_spending_cost_delta;
        public string summary;
        public List<string> effects;
    }

    [System.Serializable]
    public class WorldEventData
    {
        public string id;
        public string category;
        public string severity;
        public long tick;
        public string world_date;
        public string title;
        public string message;
        public string country_id;
        public string country_name;
        public string province_id;
        public string province_name;
        public string market_id;
        public string good_id;
        public string target_panel;
    }

    [System.Serializable]
    public class ExplanationFactorData
    {
        public string label;
        public string detail;
        public string impact;
    }

    [System.Serializable]
    public class ExplanationLinkData
    {
        public string type;
        public string id;
        public string label;
    }

    [System.Serializable]
    public class ExplanationData
    {
        public string subject_type;
        public string subject_id;
        public string title;
        public string summary;
        public string generated_at;
        public List<ExplanationFactorData> factors;
        public Dictionary<string, float> metrics;
        public List<ExplanationLinkData> related;
    }

    [System.Serializable]
    public class ArmyStackData
    {
        public string id;
        public string country_id;
        public string country_name;
        public string location_province_id;
        public string location_province_name;
        public string destination_province_id;
        public string destination_province_name;
        public int movement_ticks_remaining;
        public int soldier_count;
        public float morale;
        public bool is_moving;
    }

    [System.Serializable]
    public class WarData
    {
        public string id;
        public string attacker_country_id;
        public string attacker_country_name;
        public string defender_country_id;
        public string defender_country_name;
        public string started_at;
        public string ended_at;
        public bool is_active;
    }

    [System.Serializable]
    public class AdminProvincePopGroupData
    {
        public string id;
        public int size;
        public float population_share;
        public string pop_type;
        public string strata;
        public string culture;
        public string religion;
        public float literacy;
        public float militancy;
        public float consciousness;
        public float cash;
        public float life_needs_fulfillment;
        public float everyday_needs_fulfillment;
        public float luxury_needs_fulfillment;
        public int employed_count;
        public int unemployed_count;
    }

    [System.Serializable]
    public class AdminFactoryData
    {
        public string id;
        public string type;
        public int level;
        public string output_good;
        public float output_per_tick;
        public int employed_craftsmen;
        public int employed_clerks;
        public Dictionary<string, float> input_goods;
        public float cash_reserve;
        public float profit_last_tick;
    }

    [System.Serializable]
    public class AdminProvinceInspectorData
    {
        public string province_id;
        public string name;
        public string owner_name;
        public string rgo_type;
        public int population;
        public int workforce;
        public float needs_fulfillment;
        public Dictionary<string, float> outputs_per_tick;
        public Dictionary<string, float> local_demand;
        public List<AdminProvincePopGroupData> pop_groups;
        public List<AdminFactoryData> factories;
    }

    public interface IWorldApiClient
    {
        Task<WorldSummaryData> GetWorldSummaryAsync();
        Task<List<CountryData>> ListCountriesAsync();
        Task<List<ProvinceData>> ListProvincesAsync();
        Task<List<ProvinceData>> ListProvincesAsync(string ownerId, string sort, string order);
        Task<ProvinceDetailData> GetProvinceDetailAsync(string provinceId);
        Task<AdminCountryInspectorData> GetCountryInspectorAsync(string countryId);
        Task<AdminProvinceInspectorData> GetProvinceInspectorAsync(string provinceId);
        Task<List<BuildingQueueItemData>> GetBuildingQueueAsync();
        Task<BudgetAdjustmentPreviewData> GetBudgetAdjustmentPreviewAsync(string countryId, string kind, string target, float value);
        Task<List<ConstructionOptionPreviewData>> GetConstructionOptionsAsync(string provinceId);
        Task<List<WorldEventData>> GetEventFeedAsync(string countryId, int limit);
        Task<ExplanationData> ExplainGoodAsync(string goodId);
        Task<ExplanationData> ExplainPopNeedsAsync(string popId);
        Task<ExplanationData> ExplainProvinceEmploymentAsync(string provinceId);
        Task<ExplanationData> ExplainCountryBudgetAsync(string countryId);
        Task<ExplanationData> ExplainWarAsync(string warId);
        Task<ExplanationData> ExplainBattleAsync(string battleId);
        Task<List<ArmyStackData>> ListArmiesAsync(string countryId);
        Task<List<WarData>> ListWarsAsync();
        Task<CommandResponseData> QueueBuildingAsync(string provinceId, string buildingType);
        Task<CommandResponseData> ChangeTaxRateAsync(string countryId, int taxRate);
        Task<CommandResponseData> ChangeStrataTaxAsync(string countryId, string strata, float rate);
        Task<CommandResponseData> ChangeSpendingAsync(string countryId, string category, float level);
        Task<CommandResponseData> MoveArmyAsync(string armyId, string destinationProvinceId);
        Task<CommandResponseData> DeclareWarAsync(string targetCountryId);
        Task<CommandResponseData> MakePeaceAsync(string targetCountryId);
    }

    public class WorldApiClient : IWorldApiClient
    {
        private readonly string _baseUrl;

        public WorldApiClient(string baseUrl = "http://localhost:5001")
        {
            _baseUrl = baseUrl;
        }

        public async Task<WorldSummaryData> GetWorldSummaryAsync()
        {
            return await GetAsync<WorldSummaryData>("/api/world/summary");
        }

        public async Task<List<CountryData>> ListCountriesAsync()
        {
            return await GetAsync<List<CountryData>>("/api/world/countries");
        }

        public async Task<List<ProvinceData>> ListProvincesAsync()
        {
            return await GetAsync<List<ProvinceData>>("/api/world/provinces");
        }

        public async Task<List<ProvinceData>> ListProvincesAsync(string ownerId, string sort, string order)
        {
            var qs = new System.Collections.Generic.List<string>();
            if (!string.IsNullOrEmpty(ownerId)) qs.Add($"owner={UnityEngine.Networking.UnityWebRequest.EscapeURL(ownerId)}");
            if (!string.IsNullOrEmpty(sort)) qs.Add($"sort={UnityEngine.Networking.UnityWebRequest.EscapeURL(sort)}");
            if (!string.IsNullOrEmpty(order)) qs.Add($"order={UnityEngine.Networking.UnityWebRequest.EscapeURL(order)}");
            var query = qs.Count > 0 ? "?" + string.Join("&", qs) : "";
            return await GetAsync<List<ProvinceData>>($"/api/world/provinces{query}");
        }

        public async Task<ProvinceDetailData> GetProvinceDetailAsync(string provinceId)
        {
            return await GetAsync<ProvinceDetailData>($"/api/world/provinces/{provinceId}");
        }

        public async Task<AdminCountryInspectorData> GetCountryInspectorAsync(string countryId)
        {
            return await GetAsync<AdminCountryInspectorData>($"/api/world/countries/{countryId}/inspect");
        }

        public async Task<AdminProvinceInspectorData> GetProvinceInspectorAsync(string provinceId)
        {
            return await GetAsync<AdminProvinceInspectorData>($"/api/world/provinces/{provinceId}/inspect");
        }

        public async Task<List<BuildingQueueItemData>> GetBuildingQueueAsync()
        {
            return await GetAsync<List<BuildingQueueItemData>>("/api/world/buildings/queue");
        }

        public async Task<BudgetAdjustmentPreviewData> GetBudgetAdjustmentPreviewAsync(string countryId, string kind, string target, float value)
        {
            var url = string.Format(
                CultureInfo.InvariantCulture,
                "/api/world/countries/{0}/budget-preview?kind={1}&target={2}&value={3}",
                UnityWebRequest.EscapeURL(countryId),
                UnityWebRequest.EscapeURL(kind),
                UnityWebRequest.EscapeURL(target),
                value);
            return await GetAsync<BudgetAdjustmentPreviewData>(url);
        }

        public async Task<List<ConstructionOptionPreviewData>> GetConstructionOptionsAsync(string provinceId)
        {
            return await GetAsync<List<ConstructionOptionPreviewData>>($"/api/world/provinces/{UnityWebRequest.EscapeURL(provinceId)}/construction-options");
        }

        public async Task<List<WorldEventData>> GetEventFeedAsync(string countryId, int limit)
        {
            var qs = new System.Collections.Generic.List<string>();
            if (!string.IsNullOrEmpty(countryId)) qs.Add($"countryId={UnityWebRequest.EscapeURL(countryId)}");
            qs.Add($"limit={Mathf.Clamp(limit, 1, 100)}");
            return await GetAsync<List<WorldEventData>>($"/api/world/events?{string.Join("&", qs)}");
        }

        public async Task<ExplanationData> ExplainGoodAsync(string goodId)
        {
            return await GetAsync<ExplanationData>($"/api/explain/good/{UnityWebRequest.EscapeURL(goodId)}");
        }

        public async Task<ExplanationData> ExplainPopNeedsAsync(string popId)
        {
            return await GetAsync<ExplanationData>($"/api/explain/pop/{UnityWebRequest.EscapeURL(popId)}/needs");
        }

        public async Task<ExplanationData> ExplainProvinceEmploymentAsync(string provinceId)
        {
            return await GetAsync<ExplanationData>($"/api/explain/province/{UnityWebRequest.EscapeURL(provinceId)}/employment");
        }

        public async Task<ExplanationData> ExplainCountryBudgetAsync(string countryId)
        {
            return await GetAsync<ExplanationData>($"/api/explain/country/{UnityWebRequest.EscapeURL(countryId)}/budget");
        }

        public async Task<ExplanationData> ExplainWarAsync(string warId)
        {
            return await GetAsync<ExplanationData>($"/api/explain/war/{UnityWebRequest.EscapeURL(warId)}");
        }

        public async Task<ExplanationData> ExplainBattleAsync(string battleId)
        {
            return await GetAsync<ExplanationData>($"/api/explain/battle/{UnityWebRequest.EscapeURL(battleId)}");
        }

        public async Task<List<ArmyStackData>> ListArmiesAsync(string countryId)
        {
            var query = string.IsNullOrEmpty(countryId)
                ? string.Empty
                : $"?countryId={UnityWebRequest.EscapeURL(countryId)}";
            return await GetAsync<List<ArmyStackData>>($"/api/world/armies{query}");
        }

        public async Task<List<WarData>> ListWarsAsync()
        {
            return await GetAsync<List<WarData>>("/api/world/wars");
        }

        public async Task<CommandResponseData> QueueBuildingAsync(string provinceId, string buildingType)
        {
            var payload = $"{{\"provinceId\":\"{EscapeJson(provinceId)}\",\"buildingType\":\"{EscapeJson(buildingType)}\"}}";
            return await SubmitCommandAsync("QueueBuilding", payload);
        }

        public async Task<CommandResponseData> ChangeTaxRateAsync(string countryId, int taxRate)
        {
            var payload = string.Format(
                CultureInfo.InvariantCulture,
                "{{\"countryId\":\"{0}\",\"newTaxRate\":{1}}}",
                EscapeJson(countryId),
                Mathf.Clamp(taxRate, 0, 100));

            return await SubmitCommandAsync("ChangeTaxRate", payload);
        }

        public async Task<CommandResponseData> ChangeStrataTaxAsync(string countryId, string strata, float rate)
        {
            var payload = string.Format(
                CultureInfo.InvariantCulture,
                "{{\"countryId\":\"{0}\",\"strata\":\"{1}\",\"rate\":{2}}}",
                EscapeJson(countryId),
                EscapeJson(strata),
                rate);

            return await SubmitCommandAsync("ChangeStrataTax", payload);
        }

        public async Task<CommandResponseData> ChangeSpendingAsync(string countryId, string category, float level)
        {
            var payload = string.Format(
                CultureInfo.InvariantCulture,
                "{{\"countryId\":\"{0}\",\"category\":\"{1}\",\"level\":{2}}}",
                EscapeJson(countryId),
                EscapeJson(category),
                level);

            return await SubmitCommandAsync("ChangeSpending", payload);
        }

        public async Task<CommandResponseData> MoveArmyAsync(string armyId, string destinationProvinceId)
        {
            var payload = $"{{\"armyId\":\"{EscapeJson(armyId)}\",\"destinationProvinceId\":\"{EscapeJson(destinationProvinceId)}\"}}";
            return await SubmitCommandAsync("MoveArmy", payload);
        }

        public async Task<CommandResponseData> DeclareWarAsync(string targetCountryId)
        {
            var payload = $"{{\"targetCountryId\":\"{EscapeJson(targetCountryId)}\"}}";
            return await SubmitCommandAsync("DeclareWar", payload);
        }

        public async Task<CommandResponseData> MakePeaceAsync(string targetCountryId)
        {
            var payload = $"{{\"targetCountryId\":\"{EscapeJson(targetCountryId)}\"}}";
            return await SubmitCommandAsync("MakePeace", payload);
        }

        private async Task<T> GetAsync<T>(string endpoint)
        {
            using (var www = UnityEngine.Networking.UnityWebRequest.Get(_baseUrl + endpoint))
            {
                www.downloadHandler = new UnityEngine.Networking.DownloadHandlerBuffer();
                www.SetRequestHeader("Accept", "application/json");
                if (PlayerSession.IsLoggedIn)
                    www.SetRequestHeader("Authorization", $"Bearer {PlayerSession.Token}");

                var operation = www.SendWebRequest();
                while (!operation.isDone)
                {
                    await Task.Delay(10);
                }

                if (www.result != UnityEngine.Networking.UnityWebRequest.Result.Success)
                {
                    Debug.LogError($"API Error: {www.error}");
                    throw new Exception($"API call failed: {www.error}");
                }

                var json = www.downloadHandler.text;
                return ParseJson<T>(json);
            }
        }

        private async Task<CommandResponseData> SubmitCommandAsync(string commandType, string payloadJson)
        {
            var body = $"{{\"commandType\":\"{EscapeJson(commandType)}\",\"payload\":{payloadJson}}}";
            var bytes = Encoding.UTF8.GetBytes(body);

            using (var www = new UnityWebRequest(_baseUrl + "/api/world/commands", "POST"))
            {
                www.uploadHandler = new UploadHandlerRaw(bytes);
                www.downloadHandler = new DownloadHandlerBuffer();
                www.SetRequestHeader("Content-Type", "application/json");
                www.SetRequestHeader("Accept", "application/json");
                if (PlayerSession.IsLoggedIn)
                    www.SetRequestHeader("Authorization", $"Bearer {PlayerSession.Token}");

                var operation = www.SendWebRequest();
                while (!operation.isDone)
                {
                    await Task.Delay(10);
                }

                if (www.result != UnityWebRequest.Result.Success)
                {
                    var errorJson = www.downloadHandler.text;
                    if (!string.IsNullOrWhiteSpace(errorJson) && errorJson.TrimStart().StartsWith("{"))
                    {
                        var rejected = JsonUtility.FromJson<CommandResponseData>(errorJson);
                        if (rejected != null)
                        {
                            if (string.IsNullOrEmpty(rejected.status))
                                rejected.status = ExtractStringField(errorJson, "status") ?? "rejected";
                            if (string.IsNullOrEmpty(rejected.commandType))
                                rejected.commandType = ExtractStringField(errorJson, "commandType") ?? commandType;
                            if (string.IsNullOrEmpty(rejected.message))
                                rejected.message = ExtractStringField(errorJson, "message") ?? errorJson;
                            if (rejected.retryAfterTicks <= 0)
                                rejected.retryAfterTicks = ExtractIntField(errorJson, "retryAfterTicks");
                            return rejected;
                        }
                    }

                    if (www.responseCode == 429)
                    {
                        return new CommandResponseData
                        {
                            commandType = commandType,
                            status = "rejected",
                            message = string.IsNullOrEmpty(errorJson) ? www.error : errorJson,
                            rejectionReason = "RateLimited"
                        };
                    }

                    Debug.LogError($"Command Error: {www.error} {errorJson}");
                    throw new Exception($"Command failed: {www.error} {errorJson}");
                }

                return JsonUtility.FromJson<CommandResponseData>(www.downloadHandler.text);
            }
        }

        private static string EscapeJson(string value)
        {
            return (value ?? string.Empty)
                .Replace("\\", "\\\\")
                .Replace("\"", "\\\"");
        }

        private static string ExtractStringField(string json, string key)
        {
            var search = $"\"{key}\":\"";
            var start = json.IndexOf(search, StringComparison.Ordinal);
            if (start < 0) return null;
            start += search.Length;
            var end = json.IndexOf('"', start);
            return end >= 0 ? json.Substring(start, end - start) : null;
        }

        private static int ExtractIntField(string json, string key)
        {
            var search = $"\"{key}\":";
            var start = json.IndexOf(search, StringComparison.Ordinal);
            if (start < 0) return 0;
            start += search.Length;
            while (start < json.Length && json[start] == ' ') start++;
            var end = start;
            while (end < json.Length && char.IsDigit(json[end])) end++;
            return end > start && int.TryParse(json.Substring(start, end - start), out var value) ? value : 0;
        }

        private static T ParseJson<T>(string json)
        {
            var type = typeof(T);
            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>))
            {
                var elementType = type.GetGenericArguments()[0];
                var wrapperType = typeof(ListWrapper<>).MakeGenericType(elementType);
                var wrapped = "{\"items\":" + json + "}";
                var wrapper = JsonUtility.FromJson(wrapped, wrapperType);
                var field = wrapperType.GetField("items");
                var list = field.GetValue(wrapper);
                HydrateListDictionaries(list, elementType, json);
                return (T)list;
            }

            var parsed = JsonUtility.FromJson<T>(json);
            HydrateObjectDictionaries(parsed, json);
            return parsed;
        }

        private static void HydrateListDictionaries(object listObj, Type elementType, string json)
        {
            if (listObj == null || elementType == null)
                return;

            var list = listObj as System.Collections.IList;
            if (list == null)
                return;

            var itemJson = SplitTopLevelObjects(json);
            for (var i = 0; i < list.Count && i < itemJson.Count; i++)
                HydrateObjectDictionaries(list[i], itemJson[i]);
        }

        private static void HydrateObjectDictionaries(object target, string json)
        {
            if (target == null || string.IsNullOrEmpty(json))
                return;

            if (target is ProvinceDetailData province)
            {
                province.market_goods = ParseFloatDict(json, "market_goods");
                province.outputs_per_tick = ParseFloatDict(json, "outputs_per_tick");
                return;
            }

            if (target is ConstructionOptionPreviewData constructionOption)
            {
                constructionOption.output_per_tick = ParseFloatDict(json, "output_per_tick");
                return;
            }

            if (target is ExplanationData explanation)
            {
                explanation.metrics = ParseFloatDict(json, "metrics");
                return;
            }

            if (target is AdminProvinceInspectorData provinceInspector)
            {
                provinceInspector.outputs_per_tick = ParseFloatDict(json, "outputs_per_tick");
                provinceInspector.local_demand = ParseFloatDict(json, "local_demand");

                var factoryJson = ExtractArrayObjectJson(json, "factories");
                if (provinceInspector.factories != null)
                {
                    for (var i = 0; i < provinceInspector.factories.Count && i < factoryJson.Count; i++)
                        HydrateObjectDictionaries(provinceInspector.factories[i], factoryJson[i]);
                }
                return;
            }

            if (target is AdminFactoryData factory)
                factory.input_goods = ParseFloatDict(json, "input_goods");
        }

        private static Dictionary<string, float> ParseFloatDict(string json, string key)
        {
            var body = ExtractObjectBody(json, key);
            var result = new Dictionary<string, float>();
            if (string.IsNullOrEmpty(body))
                return result;

            foreach (var pair in SplitTopLevel(body, ','))
            {
                var separator = pair.IndexOf(':');
                if (separator <= 0)
                    continue;

                var rawKey = pair.Substring(0, separator).Trim();
                var rawValue = pair.Substring(separator + 1).Trim();
                var parsedKey = UnquoteJsonString(rawKey);
                if (string.IsNullOrEmpty(parsedKey))
                    continue;

                if (float.TryParse(rawValue, NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
                    result[parsedKey] = value;
            }

            return result;
        }

        private static string ExtractObjectBody(string json, string key)
        {
            var search = $"\"{key}\":";
            var start = json.IndexOf(search, StringComparison.Ordinal);
            if (start < 0)
                return null;

            start += search.Length;
            while (start < json.Length && char.IsWhiteSpace(json[start]))
                start++;
            if (start >= json.Length || json[start] != '{')
                return null;

            var end = FindMatching(json, start, '{', '}');
            return end > start ? json.Substring(start + 1, end - start - 1) : null;
        }

        private static List<string> ExtractArrayObjectJson(string json, string key)
        {
            var search = $"\"{key}\":";
            var start = json.IndexOf(search, StringComparison.Ordinal);
            if (start < 0)
                return new List<string>();

            start += search.Length;
            while (start < json.Length && char.IsWhiteSpace(json[start]))
                start++;
            if (start >= json.Length || json[start] != '[')
                return new List<string>();

            var end = FindMatching(json, start, '[', ']');
            return end > start ? SplitTopLevelObjects(json.Substring(start, end - start + 1)) : new List<string>();
        }

        private static List<string> SplitTopLevelObjects(string jsonArray)
        {
            var result = new List<string>();
            if (string.IsNullOrWhiteSpace(jsonArray))
                return result;

            var start = jsonArray.IndexOf('[');
            var end = jsonArray.LastIndexOf(']');
            if (start < 0 || end <= start)
                return result;

            var body = jsonArray.Substring(start + 1, end - start - 1);
            foreach (var item in SplitTopLevel(body, ','))
            {
                var trimmed = item.Trim();
                if (trimmed.StartsWith("{", StringComparison.Ordinal) && trimmed.EndsWith("}", StringComparison.Ordinal))
                    result.Add(trimmed);
            }

            return result;
        }

        private static List<string> SplitTopLevel(string value, char separator)
        {
            var result = new List<string>();
            var start = 0;
            var depth = 0;
            var inString = false;
            var escaped = false;

            for (var i = 0; i < value.Length; i++)
            {
                var c = value[i];
                if (escaped)
                {
                    escaped = false;
                    continue;
                }

                if (c == '\\' && inString)
                {
                    escaped = true;
                    continue;
                }

                if (c == '"')
                {
                    inString = !inString;
                    continue;
                }

                if (inString)
                    continue;

                if (c == '{' || c == '[')
                    depth++;
                else if (c == '}' || c == ']')
                    depth--;
                else if (c == separator && depth == 0)
                {
                    result.Add(value.Substring(start, i - start));
                    start = i + 1;
                }
            }

            result.Add(value.Substring(start));
            return result;
        }

        private static int FindMatching(string value, int openIndex, char open, char close)
        {
            var depth = 0;
            var inString = false;
            var escaped = false;

            for (var i = openIndex; i < value.Length; i++)
            {
                var c = value[i];
                if (escaped)
                {
                    escaped = false;
                    continue;
                }

                if (c == '\\' && inString)
                {
                    escaped = true;
                    continue;
                }

                if (c == '"')
                {
                    inString = !inString;
                    continue;
                }

                if (inString)
                    continue;

                if (c == open)
                    depth++;
                else if (c == close)
                {
                    depth--;
                    if (depth == 0)
                        return i;
                }
            }

            return -1;
        }

        private static string UnquoteJsonString(string value)
        {
            value = value?.Trim();
            if (string.IsNullOrEmpty(value))
                return null;
            if (value.Length >= 2 && value[0] == '"' && value[value.Length - 1] == '"')
                value = value.Substring(1, value.Length - 2);

            return value
                .Replace("\\\"", "\"")
                .Replace("\\\\", "\\");
        }

        [System.Serializable]
        private class ListWrapper<T>
        {
            public List<T> items;
        }
    }

    [System.Serializable]
    public class CommandResponseData
    {
        public string commandId;
        public string actorId;
        public string commandType;
        public string status;
        public string message;
        public string rejectionReason;
        public bool softLimited;
        public int remainingInWindow;
        public int retryAfterTicks;
        public float retryAfterSeconds;
    }
}
