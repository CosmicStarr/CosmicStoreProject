namespace Models;

/// <summary>
/// Current storefront legal documents. Checkout and registration must send this version.
/// </summary>
public static class LegalTerms
{
    public const string CurrentVersion = "2026-09-21.3";

    public static DateTime RequireAcceptance(string? version, DateTimeOffset? acceptedAt)
    {
        if (!string.Equals(version?.Trim(), CurrentVersion, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "You must agree to the current Terms & Conditions and Privacy Policy.");
        }

        if (acceptedAt is null || acceptedAt.Value == default)
        {
            throw new InvalidOperationException(
                "You must agree to the Terms & Conditions and Privacy Policy.");
        }

        var utc = acceptedAt.Value.UtcDateTime;
        if (utc > DateTime.UtcNow.AddMinutes(10) || utc < DateTime.UtcNow.AddDays(-7))
        {
            throw new InvalidOperationException(
                "You must agree to the Terms & Conditions and Privacy Policy.");
        }

        return utc;
    }
}
