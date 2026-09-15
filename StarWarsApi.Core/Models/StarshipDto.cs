using System.Text.Json.Serialization;

namespace StarWarsApi.Core.Models;

public sealed class StarshipDto
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("length")]
    public string? Length { get; set; }

    [JsonPropertyName("pilots")]
    public List<string> Pilots { get; set; } = [];
}
