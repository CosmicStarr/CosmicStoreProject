using System.ComponentModel.DataAnnotations;

namespace Models.AngularDTOs;

public class ShippingQuoteRequest
{
    [Required]
    public string CountryCode { get; set; } = string.Empty;

    public string? ProvinceOrState { get; set; }

    public string? City { get; set; }

    public List<ShippingQuoteItem> Items { get; set; } = new();
}

public class ShippingQuoteItem
{
    public string Sku { get; set; } = string.Empty;
    public int Amount { get; set; }
}
