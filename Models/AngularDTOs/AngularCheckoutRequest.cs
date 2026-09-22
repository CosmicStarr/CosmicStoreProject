using System.ComponentModel.DataAnnotations;

namespace Models.AngularDTOs;

public class AngularCheckoutRequest
{
    [Required, EmailAddress]
    public string Email { get; set; } = string.Empty;

    public string FullName { get; set; } = string.Empty;
    public string StreetAddress { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string ProvinceOrState { get; set; } = string.Empty;

    [MaxLength(20)]
    public string ZipCode { get; set; } = string.Empty;

    public string CountryCode { get; set; } = string.Empty;

    public int? WishlistId { get; set; }

    public string StripePaymentMethodId { get; set; } = string.Empty;

    public string? LogisticName { get; set; }
    public decimal ShippingCost { get; set; }

    public List<CartItems> Items { get; set; } = new();

    public string? AcceptedTermsVersion { get; set; }
    public DateTimeOffset? AcceptedTermsAt { get; set; }
}