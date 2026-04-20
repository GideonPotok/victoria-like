using System.Threading.Tasks;
using UnityEngine;
using VictoriaLike.Client.Api;
using VictoriaLike.Client.UI;

namespace VictoriaLike.Client
{
    public class Bootstrap : MonoBehaviour
    {
        [Header("Config")]
        [SerializeField] private string serverUrl = "http://localhost:5001";
        [SerializeField] private string defaultUsername = "albion-player";
        [SerializeField] private string defaultPassword = "alb123";

        [Header("Scene references")]
        [SerializeField] private WorldWebSocketClient wsClient;
        [SerializeField] private WorldUIManager worldUIManager;

        private void Start()
        {
            _ = InitializeAsync();
        }

        private async Task InitializeAsync()
        {
            Debug.Log($"[Bootstrap] Starting. Server: {serverUrl}");

            // 1. Login
            var auth = new AuthApiClient(serverUrl);
            try
            {
                await auth.LoginAsync(defaultUsername, defaultPassword);
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[Bootstrap] Login failed: {ex.Message}");
                return;
            }

            // 2. Connect WebSocket (WorldWebSocketClient.Start already calls Connect if logged in,
            //    but Bootstrap may run before it, so connect explicitly if needed)
            if (wsClient != null && !PlayerSession.IsLoggedIn == false)
                wsClient.Connect();

            // 3. Fetch initial REST snapshot
            if (worldUIManager != null)
                await worldUIManager.FetchInitialSnapshotAsync();

            Debug.Log("[Bootstrap] Ready.");
        }
    }
}
