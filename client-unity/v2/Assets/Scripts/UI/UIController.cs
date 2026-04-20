using System;
using UnityEngine;
using VictoriaLike.Client.Api;

namespace VictoriaLike.Client.UI
{
    public class UIController : MonoBehaviour
    {
        [SerializeField] private ProvinceListUI provinceListUI;
        [SerializeField] private ProvinceDetailUI provinceDetailUI;

        private void Start()
        {
            // Wire up events
            if (provinceListUI != null)
            {
                provinceListUI.OnProvinceSelected += OnProvinceSelected;
            }

            if (provinceDetailUI != null)
            {
                provinceDetailUI.OnBackClicked += OnBackClicked;
            }
        }

        private void OnProvinceSelected(ProvinceData province)
        {
            Debug.Log($"Province selected: {province.name}");

            // Hide list, show detail
            if (provinceListUI != null)
            {
                provinceListUI.gameObject.SetActive(false);
            }

            if (provinceDetailUI != null)
            {
                _ = provinceDetailUI.ShowProvinceDetailAsync(province);
            }
        }

        private void OnBackClicked()
        {
            Debug.Log("Returning to province list");

            // Hide detail, show list
            if (provinceDetailUI != null)
            {
                provinceDetailUI.Hide();
            }

            if (provinceListUI != null)
            {
                provinceListUI.gameObject.SetActive(true);
            }
        }

        private void OnDestroy()
        {
            if (provinceListUI != null)
            {
                provinceListUI.OnProvinceSelected -= OnProvinceSelected;
            }

            if (provinceDetailUI != null)
            {
                provinceDetailUI.OnBackClicked -= OnBackClicked;
            }
        }
    }
}
