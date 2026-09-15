using StarWarsApi.Core.Models;

namespace StarWarsApi.Core;

public static class StarshipPrinter
{
    public static string FormatPilots(StarshipWithPilots ship)
        => ship.PilotNames.Count > 0 ? string.Join(", ", ship.PilotNames) : "(none)";

    public static string Format(StarshipWithPilots ship)
        => $"{ship.Name} | Length: {ship.LengthRaw} | Pilots: {FormatPilots(ship)}";
}
