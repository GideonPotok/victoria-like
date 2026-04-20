using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using VictoriaLike.Client.Api;

namespace VictoriaLike.Client.UI
{
    public class ProvinceListUI : MonoBehaviour
    {
        [SerializeField] private Transform provincesContainer;
        [SerializeField] private Button refreshButton;
        [SerializeField] private Button provincePrefab;
        [SerializeField] private Text loadingText;
        [SerializeField] private Button sortByNameButton;
        [SerializeField] private Button sortByPopulationButton;
        [SerializeField] private Button filterMineButton;
        [SerializeField] private Button clearFilterButton;

        private IWorldApiClient _apiClient;
        private List<ProvinceData> _provinces;
        private string _sort = "name";
        private string _order = "asc";
        private string _filterOwnerId = null;
        public event Action<ProvinceData> OnProvinceSelected;

        private void Start()
        {
            _apiClient = new WorldApiClient("http://localhost:5001");
            _provinces = new List<ProvinceData>();

            if (refreshButton != null)
                refreshButton.onClick.AddListener(() => _ = RefreshProvincesAsync());
            if (sortByNameButton != null)
                sortByNameButton.onClick.AddListener(() => { ToggleSort("name"); _ = RefreshProvincesAsync(); });
            if (sortByPopulationButton != null)
                sortByPopulationButton.onClick.AddListener(() => { ToggleSort("population"); _ = RefreshProvincesAsync(); });
            if (filterMineButton != null)
                filterMineButton.onClick.AddListener(() => { _filterOwnerId = PlayerSession.ControlledCountryId; _ = RefreshProvincesAsync(); });
            if (clearFilterButton != null)
                clearFilterButton.onClick.AddListener(() => { _filterOwnerId = null; _ = RefreshProvincesAsync(); });

            _ = RefreshProvincesAsync();
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

        public async Task RefreshProvincesAsync()
        {
            try
            {
                if (loadingText != null)
                    loadingText.text = "Loading provinces...";

                _provinces = await _apiClient.ListProvincesAsync(_filterOwnerId, _sort, _order);

                UpdateProvinceList();

                if (loadingText != null)
                    loadingText.text = "";

                Debug.Log($"Loaded {_provinces.Count} provinces");
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error loading provinces: {ex.Message}");
                if (loadingText != null)
                    loadingText.text = $"Error: {ex.Message}";
            }
        }

        private void UpdateProvinceList()
        {
            // Clear existing buttons
            foreach (Transform child in provincesContainer)
            {
                if (child.gameObject != provincePrefab.gameObject)
                {
                    Destroy(child.gameObject);
                }
            }

            // Create buttons for each province
            foreach (var province in _provinces)
            {
                var button = Instantiate(provincePrefab, provincesContainer);
                button.gameObject.SetActive(true);

                var text = button.GetComponentInChildren<Text>();
                if (text != null)
                {
                    text.text = $"{province.name} ({province.owner_name}) - Pop: {province.population}";
                }

                button.onClick.AddListener(() => SelectProvince(province));
            }
        }

        private void SelectProvince(ProvinceData province)
        {
            Debug.Log($"Selected province: {province.name}");
            OnProvinceSelected?.Invoke(province);
        }
    }
}
