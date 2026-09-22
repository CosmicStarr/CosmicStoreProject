namespace Models.AngularDTOs;

public class WishlistItemDto
{
    public int Id { get; set; }
    public string ProductId { get; set; } = string.Empty;
    public string NameEn { get; set; } = string.Empty;
    public string Sku { get; set; } = string.Empty;
    public decimal SellPrice { get; set; }
    public string? BigImage { get; set; }
    public DateTime AddedAt { get; set; }
}

public class WishlistDto
{
    public int Id { get; set; }
    public string PublicId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int? ShippingAddressId { get; set; }
    public bool IsGiftRegistry { get; set; }
    public bool IsAddressPrivate { get; set; }
    public string? MaskedShippingLabel { get; set; }
    public string? ShareUrl { get; set; }
    public string FulfillmentDisclaimer { get; set; } = string.Empty;
    public List<WishlistItemDto> Items { get; set; } = new();
}

/// <summary>Public registry payload. Must never include street, city, zip, or address ids.</summary>
public class PublicWishlistDto
{
    public int Id { get; set; }
    public string PublicId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string MaskedShippingLabel { get; set; } = string.Empty;
    public string FulfillmentDisclaimer { get; set; } = string.Empty;
    public List<WishlistItemDto> Items { get; set; } = new();
}

public class UpdateWishlistRequest
{
    public string? Name { get; set; }
    public int? ShippingAddressId { get; set; }
    public bool IsGiftRegistry { get; set; }
}
