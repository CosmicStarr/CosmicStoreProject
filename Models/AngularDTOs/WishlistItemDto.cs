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
