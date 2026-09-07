using System.Text.Json.Serialization;

namespace Models;

public class CjAuthTokenData
{
    [JsonPropertyName("accessToken")]
    public required string AccessToken { get; set; }

    [JsonPropertyName("refreshToken")]
    public required string RefreshToken { get; set; }

}