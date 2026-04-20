using System;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace VictoriaLike.Client.Api
{
    [Serializable]
    public class LoginRequest
    {
        public string username;
        public string password;
    }

    [Serializable]
    public class LoginResponse
    {
        public string token;
        public string actor_id;
        public string username;
        public string controlled_country_id;
    }

    public class AuthApiClient
    {
        private readonly string _baseUrl;

        public AuthApiClient(string baseUrl = "http://localhost:5001")
        {
            _baseUrl = baseUrl;
        }

        public async Task<LoginResponse> LoginAsync(string username, string password)
        {
            var body = JsonUtility.ToJson(new LoginRequest { username = username, password = password });
            var bytes = Encoding.UTF8.GetBytes(body);

            using var www = new UnityWebRequest(_baseUrl + "/api/auth/login", "POST");
            www.uploadHandler = new UploadHandlerRaw(bytes);
            www.downloadHandler = new DownloadHandlerBuffer();
            www.SetRequestHeader("Content-Type", "application/json");
            www.SetRequestHeader("Accept", "application/json");

            var op = www.SendWebRequest();
            while (!op.isDone)
                await Task.Delay(10);

            if (www.result != UnityWebRequest.Result.Success)
                throw new Exception($"Login failed: {www.error} — {www.downloadHandler.text}");

            var response = JsonUtility.FromJson<LoginResponse>(www.downloadHandler.text);
            if (string.IsNullOrEmpty(response?.token))
                throw new Exception("Login succeeded but no token returned");

            PlayerSession.Set(response.token, response.actor_id, response.username, response.controlled_country_id);
            Debug.Log($"[Auth] Logged in as {response.username} (actor {response.actor_id})");
            return response;
        }

        public async Task LogoutAsync()
        {
            if (!PlayerSession.IsLoggedIn)
                return;

            using var www = new UnityWebRequest(_baseUrl + "/api/auth/logout", "POST");
            www.downloadHandler = new DownloadHandlerBuffer();
            www.SetRequestHeader("Authorization", $"Bearer {PlayerSession.Token}");

            var op = www.SendWebRequest();
            while (!op.isDone)
                await Task.Delay(10);

            PlayerSession.Clear();
            Debug.Log("[Auth] Logged out");
        }
    }
}
