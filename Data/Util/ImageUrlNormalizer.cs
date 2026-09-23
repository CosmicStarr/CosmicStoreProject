using System.Text.Json;

namespace Data.Util;

/// <summary>
/// CJ sometimes sends product images as a JSON array string (or a JSON array element).
/// Storefront/admin fields expect a single http(s) URL.
/// </summary>
public static class ImageUrlNormalizer
{
    /// <summary>Returns the first usable image URL from a CJ/admin value, or null.</summary>
    public static string? First(string? raw)
    {
        var urls = All(raw);
        return urls.Count > 0 ? urls[0] : null;
    }

    /// <summary>Extracts distinct http(s) image URLs from a plain URL, JSON array, or raw JSON text.</summary>
    public static IReadOnlyList<string> All(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return [];
        }

        var trimmed = raw.Trim();
        if (LooksLikeUrl(trimmed))
        {
            return [trimmed];
        }

        if (trimmed.StartsWith('[') || trimmed.StartsWith('{'))
        {
            try
            {
                using var document = JsonDocument.Parse(trimmed);
                var found = Collect(document.RootElement);
                if (found.Count > 0)
                {
                    return found;
                }
            }
            catch (JsonException)
            {
                // Fall through to delimiter splitting.
            }
        }

        return trimmed
            .Split([',', ';', '\n', '\r', '\t', ' '], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(LooksLikeUrl)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <summary>Reads a CJ JSON property that may be a string URL or an array of URLs.</summary>
    public static string? FromJson(JsonElement obj, string name)
    {
        if (!obj.TryGetProperty(name, out var property))
        {
            return null;
        }

        return property.ValueKind switch
        {
            JsonValueKind.String => First(property.GetString()),
            JsonValueKind.Array => Collect(property).FirstOrDefault(),
            JsonValueKind.Null or JsonValueKind.Undefined => null,
            _ => First(property.ToString())
        };
    }

    /// <summary>Reads every image URL from a CJ JSON property (string or array).</summary>
    public static IReadOnlyList<string> AllFromJson(JsonElement obj, string name)
    {
        if (!obj.TryGetProperty(name, out var property))
        {
            return [];
        }

        return property.ValueKind switch
        {
            JsonValueKind.String => All(property.GetString()),
            JsonValueKind.Array => Collect(property),
            JsonValueKind.Null or JsonValueKind.Undefined => [],
            _ => All(property.ToString())
        };
    }

    private static List<string> Collect(JsonElement element)
    {
        var urls = new List<string>();
        CollectInto(element, urls);
        return urls
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static void CollectInto(JsonElement element, List<string> urls)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.String:
            {
                var value = element.GetString();
                if (string.IsNullOrWhiteSpace(value))
                {
                    return;
                }

                if (LooksLikeUrl(value))
                {
                    urls.Add(value.Trim());
                    return;
                }

                urls.AddRange(All(value));
                break;
            }
            case JsonValueKind.Array:
                foreach (var child in element.EnumerateArray())
                {
                    CollectInto(child, urls);
                }
                break;
            case JsonValueKind.Object:
                foreach (var property in element.EnumerateObject())
                {
                    CollectInto(property.Value, urls);
                }
                break;
        }
    }

    private static bool LooksLikeUrl(string value) =>
        value.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
        || value.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
        || value.StartsWith("//");
}
