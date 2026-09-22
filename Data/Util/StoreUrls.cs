using Microsoft.Extensions.Configuration;

namespace Data.Util;

/// <summary>
/// Resolves public storefront URLs from Store:PublicOrigin plus ReturnPath entries
/// that may be either absolute or root-relative.
/// </summary>
public static class StoreUrls
{
    public const string PublicOriginKey = "Store:PublicOrigin";

    public static string PublicOrigin(IConfiguration configuration)
    {
        var origin = configuration[PublicOriginKey]?.Trim().TrimEnd('/');
        return string.IsNullOrWhiteSpace(origin) ? "http://127.0.0.1:4200" : origin;
    }

    /// <summary>Absolute URL to the storefront brand mark used in HTML emails.</summary>
    public static string LogoUrl(IConfiguration configuration) =>
        Combine(PublicOrigin(configuration), "/images/cosmicstore-logo.png");

    public static string Absolute(IConfiguration configuration, string configKey, string fallbackPath)
    {
        var configured = configuration[configKey]?.Trim();
        if (!string.IsNullOrWhiteSpace(configured))
        {
            if (IsAbsolute(configured))
            {
                return configured.TrimEnd('/');
            }

            return Combine(PublicOrigin(configuration), configured);
        }

        return Combine(PublicOrigin(configuration), fallbackPath);
    }

    public static string[] CorsOrigins(IConfiguration configuration, bool isDevelopment)
    {
        var configured = configuration.GetSection("Store:CorsOrigins").Get<string[]>()?
            .Where(origin => !string.IsNullOrWhiteSpace(origin))
            .Select(origin => origin.Trim().TrimEnd('/'))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray() ?? [];

        if (configured.Length > 0)
        {
            return configured;
        }

        if (isDevelopment)
        {
            return
            [
                "http://localhost:4200",
                "https://localhost:4200",
                "http://127.0.0.1:4200",
                "https://127.0.0.1:4200"
            ];
        }

        return [PublicOrigin(configuration)];
    }

    private static bool IsAbsolute(string value) =>
        value.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
        || value.StartsWith("https://", StringComparison.OrdinalIgnoreCase);

    private static string Combine(string origin, string path)
    {
        var relative = path.StartsWith('/') ? path : "/" + path;
        return origin.TrimEnd('/') + relative.TrimEnd('/');
    }
}
