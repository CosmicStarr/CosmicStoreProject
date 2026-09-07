using System.Text.Json.Serialization;

namespace Models;


public class CjAuthRequest
{
    public required string ApiKey { get; set; }

    public required string BaseUrl { get; set; }
}