namespace Models;


public class Products
{
    // NO ?: This is the Primary Key. It cannot be null.
    public string Id { get; set; } = string.Empty; 
    // NO ?: Let's say you require a Name and SKU in your DB.
    public string NameEn { get; set; } = string.Empty;
    public string? DescriptionEn { get; set; } // YES ?: Allow NULL for optional description
    public bool IsFeatured { get; set; }
    public bool IsNewArrival { get; set;}
    public bool IsTopSelling { get; set; }
    public string Sku { get; set; } = string.Empty;
    /// <summary>CJ Dropshipping variant ID used when placing orders.</summary>
    public string? CjVariantId { get; set; }
    // NO ?: Always required
    public decimal SellPrice { get; set; } 

    // YES ?: An image might be missing, so allow NULL in the database
    public string? BigImage { get; set; } 

    // YES ?: A category ID might be missing, allow NULL
    public string? Category { get; set; }
    // If you added these earlier, make sure they are nullable too
    public IList<ProductImage>? ProductImages { get; set; }
}