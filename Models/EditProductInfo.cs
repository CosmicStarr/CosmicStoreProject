namespace Models;

public class EditProductInfo
{
    public required string Id { get; set; } 
    public string? NameEn { get; set; } 
    public string? Sku { get; set; } 
    public string? DescriptionEn { get; set; }
    public bool IsFeatured { get; set; }
    public bool IsNewArrival { get; set;}
    public bool IsTopSelling { get; set; }
    public decimal SellPrice { get; set; }
    public string? BigImage { get; set; }
    public string? Category { get; set; }
    public IList<ProductImage>? ProductImages { get; set; }
}