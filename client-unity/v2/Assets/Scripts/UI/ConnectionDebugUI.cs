using UnityEngine;
using UnityEngine.UI;
using VictoriaLike.Client.Api;

namespace VictoriaLike.Client.UI
{
    public class ConnectionDebugUI : MonoBehaviour
    {
        [SerializeField] private Text debugLabel;
        [SerializeField] private WorldWebSocketClient wsClient;

        private void Update()
        {
            if (debugLabel == null || wsClient == null)
                return;

            var state = wsClient.ConnectionState;
            var stateStr = state switch
            {
                WsConnectionState.Connected    => "<color=green>Connected</color>",
                WsConnectionState.Connecting   => "<color=yellow>Connecting</color>",
                WsConnectionState.Reconnecting => "<color=orange>Reconnecting</color>",
                _                              => "<color=red>Disconnected</color>"
            };

            var user = PlayerSession.IsLoggedIn ? PlayerSession.Username : "not logged in";

            debugLabel.text =
                $"WS: {stateStr}  |  Tick: {wsClient.LastTickSeen}  |  Date: {wsClient.LastWorldDate}  |  User: {user}";
        }
    }
}
