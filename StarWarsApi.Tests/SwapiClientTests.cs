using System.Net;
using System.Text;
using StarWarsApi.Core;
using Xunit;

namespace StarWarsApi.Tests;

public class SwapiClientTests
{
    [Fact]
    public async Task GetStarshipsAtLeastLengthAsync_pages_filters_and_resolves_pilots()
    {
        var handler = new ScriptedHandler(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["https://swapi.dev/api/starships/"] = """
                {
                  "count": 3,
                  "next": "https://swapi.dev/api/starships/?page=2",
                  "previous": null,
                  "results": [
                    {
                      "name": "Tiny Fighter",
                      "length": "9.2",
                      "pilots": ["https://swapi.dev/api/people/4/"]
                    },
                    {
                      "name": "Millennium Falcon",
                      "length": "34.37",
                      "pilots": [
                        "https://swapi.dev/api/people/13/",
                        "https://swapi.dev/api/people/14/"
                      ]
                    },
                    {
                      "name": "Mystery Barge",
                      "length": "unknown",
                      "pilots": []
                    }
                  ]
                }
                """,
            ["https://swapi.dev/api/starships/?page=2"] = """
                {
                  "count": 3,
                  "next": null,
                  "previous": "https://swapi.dev/api/starships/",
                  "results": [
                    {
                      "name": "Star Destroyer",
                      "length": "1,600",
                      "pilots": []
                    }
                  ]
                }
                """,
            ["https://swapi.dev/api/people/13/"] = """{ "name": "Chewbacca" }""",
            ["https://swapi.dev/api/people/14/"] = """{ "name": "Han Solo" }"""
        });

        using var http = new HttpClient(handler)
        {
            BaseAddress = new Uri(SwapiClient.DefaultBaseUrl)
        };
        using var client = new SwapiClient(http);

        var ships = await client.GetStarshipsAtLeastLengthAsync(minLength: 10);

        Assert.Equal(2, ships.Count);
        Assert.Equal("Millennium Falcon", ships[0].Name);
        Assert.Equal(34.37, ships[0].Length, precision: 5);
        Assert.Equal(new[] { "Chewbacca", "Han Solo" }, ships[0].PilotNames);
        Assert.Equal("Star Destroyer", ships[1].Name);
        Assert.Equal(1600, ships[1].Length);
        Assert.Empty(ships[1].PilotNames);
        Assert.DoesNotContain(ships, s => s.Name is "Tiny Fighter" or "Mystery Barge");
        Assert.False(handler.RequestedUrls.Contains("https://swapi.dev/api/people/4/"));
    }

    private sealed class ScriptedHandler : HttpMessageHandler
    {
        private readonly Dictionary<string, string> _responses;

        public ScriptedHandler(Dictionary<string, string> responses)
        {
            _responses = responses;
        }

        public HashSet<string> RequestedUrls { get; } = new(StringComparer.OrdinalIgnoreCase);

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var url = request.RequestUri?.ToString() ?? string.Empty;
            RequestedUrls.Add(url);

            if (!_responses.TryGetValue(url, out var body))
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound)
                {
                    RequestMessage = request,
                    Content = new StringContent($"No scripted response for {url}", Encoding.UTF8, "text/plain")
                });
            }

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                RequestMessage = request,
                Content = new StringContent(body, Encoding.UTF8, "application/json")
            });
        }
    }
}
