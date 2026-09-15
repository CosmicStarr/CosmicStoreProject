using StarWarsApi.Core;

using var client = new SwapiClient();
var ships = await client.GetStarshipsAtLeastLengthAsync(minLength: 10);

Console.WriteLine($"Starships with length >= 10 ({ships.Count}):");
Console.WriteLine();

foreach (var ship in ships)
{
    Console.WriteLine(StarshipPrinter.Format(ship));
}
