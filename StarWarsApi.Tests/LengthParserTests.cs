using StarWarsApi.Core;
using Xunit;

namespace StarWarsApi.Tests;

public class LengthParserTests
{
    [Theory]
    [InlineData("150", 150)]
    [InlineData("34.37", 34.37)]
    [InlineData("1,600", 1600)]
    [InlineData("9.2", 9.2)]
    [InlineData(" 12.5 ", 12.5)]
    [InlineData("19000", 19000)]
    public void TryParseLength_parses_valid_swapi_strings(string raw, double expected)
    {
        Assert.True(SwapiClient.TryParseLength(raw, out var length));
        Assert.Equal(expected, length, precision: 5);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("unknown")]
    [InlineData("Unknown")]
    [InlineData("n/a")]
    [InlineData("N/A")]
    [InlineData("none")]
    [InlineData("null")]
    [InlineData("abc")]
    [InlineData("--")]
    public void TryParseLength_skips_unknown_or_invalid_values(string? raw)
    {
        Assert.False(SwapiClient.TryParseLength(raw, out _));
    }
}
