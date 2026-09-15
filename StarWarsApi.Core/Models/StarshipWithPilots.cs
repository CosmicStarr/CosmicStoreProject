namespace StarWarsApi.Core.Models;

public sealed record StarshipWithPilots(
    string Name,
    double Length,
    string LengthRaw,
    IReadOnlyList<string> PilotNames);
