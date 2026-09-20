using Models.AngularDTOs;

namespace Models;

public class EditProductInfo
{
    public string Id { get; set; } = string.Empty; 
    public string? NameEn { get; set; } 
    public string? Sku { get; set; } 
    public string? DescriptionEn { get; set; }
    public string? ShortDescription { get; set; }
    public bool IsFeatured { get; set; }
    public bool IsNewArrival { get; set;}
    public bool IsTopSelling { get; set; }
    public decimal SellPrice { get; set; }
    public string? BigImage { get; set; }
    public string? Category { get; set; }
    public int StockQuantity { get; set; }
    public IList<PictureDto>? ProductImages { get; set; }

    /// <summary>CJ product id (pid) entered on Add Product. Variants are keyed by vid, not this value.</summary>
    public string? CjProductId { get; set; }

    /// <summary>CJ variants to persist as ProductVariant rows (vid + sku).</summary>
    public IList<CjVariantDto>? Variants { get; set; }
}
