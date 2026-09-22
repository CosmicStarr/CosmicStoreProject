namespace Data.Util;

/// <summary>Masks registry destinations so buyers never receive a physical address.</summary>
public static class WishlistPrivacy
{
    public const string FulfillmentDisclaimer =
        "Your registry address is hidden from the public and from buyers. It is still shared with CosmicStore and our third-party logistics partners, including CJ Dropshipping, so gifts can be delivered.";

    public static string MaskedShippingLabel(string? recipientFullName)
    {
        var first = FirstName(recipientFullName);
        return $"Ship to {first}'s Registry Address";
    }

    public static string FirstName(string? fullName)
    {
        if (string.IsNullOrWhiteSpace(fullName))
        {
            return "the recipient";
        }

        var parts = fullName.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length == 0 ? "the recipient" : parts[0];
    }
}
