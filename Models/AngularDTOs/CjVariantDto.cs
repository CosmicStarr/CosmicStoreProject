namespace Models.AngularDTOs;

public class CjVariantDto
{
    public string Vid { get; set; } = string.Empty;
    public string Sku { get; set; } = string.Empty;
    public string? VariantName { get; set; }
    public decimal SellPrice { get; set; }
    public int StockQuantity { get; set; }
    public string? ImageUrl { get; set; }
}
