using System.Globalization;
using System.Net.Http.Headers;
using System.Text.Json;
using StarWarsApi.Core.Models;

namespace StarWarsApi.Core;

public sealed class SwapiClient : IDisposable
{
    public const string DefaultBaseUrl = "https://swapi.dev/api/";
    public const double DefaultMinLength = 10;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly HttpClient _http;
    private readonly bool _ownsHttp;

    public SwapiClient(HttpClient? httpClient = null, string? baseUrl = null)
    {
        _ownsHttp = httpClient is null;
        _http = httpClient ?? CreateDefaultClient();
        if (_http.BaseAddress is null)
        {
            _http.BaseAddress = new Uri(baseUrl ?? DefaultBaseUrl);
        }
    }

    public async Task<IReadOnlyList<StarshipWithPilots>> GetStarshipsAtLeastLengthAsync(
        double minLength = DefaultMinLength,
        CancellationToken cancellationToken = default)
    {
        var matches = new List<StarshipWithPilots>();
        var pilotCache = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        var visitedPages = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        string? nextUrl = "starships/";

        while (!string.IsNullOrWhiteSpace(nextUrl) && visitedPages.Add(nextUrl))
        {
            var page = await GetJsonAsync<PagedResult<StarshipDto>>(nextUrl, cancellationToken)
                .ConfigureAwait(false);

            if (page?.Results is null)
            {
                break;
            }

            foreach (var ship in page.Results)
            {
                if (!TryParseLength(ship.Length, out var length) || length < minLength)
                {
                    continue;
                }

                var pilots = new List<string>();
                foreach (var pilotUrl in ship.Pilots)
                {
                    if (string.IsNullOrWhiteSpace(pilotUrl))
                    {
                        continue;
                    }

                    var name = await ResolvePilotNameAsync(pilotUrl, pilotCache, cancellationToken)
                        .ConfigureAwait(false);
                    if (!string.IsNullOrWhiteSpace(name))
                    {
                        pilots.Add(name);
                    }
                }

                matches.Add(new StarshipWithPilots(
                    string.IsNullOrWhiteSpace(ship.Name) ? "(unnamed)" : ship.Name.Trim(),
                    length,
                    ship.Length!.Trim(),
                    pilots));
            }

            nextUrl = page.Next;
        }

        return matches;
    }

    public static bool TryParseLength(string? raw, out double length)
    {
        length = 0;
        if (string.IsNullOrWhiteSpace(raw))
        {
            return false;
        }

        var trimmed = raw.Trim();
        if (trimmed.Equals("unknown", StringComparison.OrdinalIgnoreCase)
            || trimmed.Equals("n/a", StringComparison.OrdinalIgnoreCase)
            || trimmed.Equals("none", StringComparison.OrdinalIgnoreCase)
            || trimmed.Equals("null", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        trimmed = trimmed.Replace(",", "", StringComparison.Ordinal);

        return double.TryParse(trimmed, NumberStyles.Float, CultureInfo.InvariantCulture, out length)
               && !double.IsNaN(length)
               && !double.IsInfinity(length);
    }

    private async Task<string?> ResolvePilotNameAsync(
        string pilotUrl,
        Dictionary<string, string?> cache,
        CancellationToken cancellationToken)
    {
        if (cache.TryGetValue(pilotUrl, out var cached))
        {
            return cached;
        }

        try
        {
            var person = await GetJsonAsync<PersonDto>(pilotUrl, cancellationToken).ConfigureAwait(false);
            var name = string.IsNullOrWhiteSpace(person?.Name) ? null : person.Name.Trim();
            cache[pilotUrl] = name;
            return name;
        }
        catch (HttpRequestException)
        {
            cache[pilotUrl] = null;
            return null;
        }
        catch (JsonException)
        {
            cache[pilotUrl] = null;
            return null;
        }
    }

    private async Task<T?> GetJsonAsync<T>(string url, CancellationToken cancellationToken)
    {
        using var response = await _http.GetAsync(url, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        return await JsonSerializer.DeserializeAsync<T>(stream, JsonOptions, cancellationToken).ConfigureAwait(false);
    }

    private static HttpClient CreateDefaultClient()
    {
        var client = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(60)
        };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("StarWarsApi/1.0 (+https://swapi.dev/)");
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        return client;
    }

    public void Dispose()
    {
        if (_ownsHttp)
        {
            _http.Dispose();
        }
    }
}
