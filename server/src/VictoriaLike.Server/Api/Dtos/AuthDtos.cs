using System.Text.Json.Serialization;

namespace VictoriaLike.Server.Api.Dtos;

public class LoginRequest
{
    [JsonPropertyName("username")]
    public string Username { get; set; } = string.Empty;

    [JsonPropertyName("password")]
    public string Password { get; set; } = string.Empty;
}

public class LoginResponse
{
    [JsonPropertyName("token")]
    public string Token { get; set; } = string.Empty;

    [JsonPropertyName("actor_id")]
    public string ActorId { get; set; } = string.Empty;

    [JsonPropertyName("username")]
    public string Username { get; set; } = string.Empty;

    [JsonPropertyName("controlled_country_id")]
    public string ControlledCountryId { get; set; } = string.Empty;
}

public class MeResponse
{
    [JsonPropertyName("actor_id")]
    public string ActorId { get; set; } = string.Empty;

    [JsonPropertyName("username")]
    public string Username { get; set; } = string.Empty;

    [JsonPropertyName("controlled_country_id")]
    public string ControlledCountryId { get; set; } = string.Empty;
}
