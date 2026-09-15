using System.Text.Json.Serialization;

namespace StarWarsApi.Core.Models;

public sealed class PersonDto
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }
}
