using System.ComponentModel.DataAnnotations.Schema;

namespace Models;

public class FlatProduct
{
    // NO ?: This is the Primary Key. It cannot be null.
    public string Id { get; set; } = string.Empty; 
    // NO ?: Let's say you require a Name and SKU in your DB.
    public string NameEn { get; set; } = string.Empty;
    public string Sku { get; set; } = string.Empty;
    
    // NO ?: Always required
    public decimal SellPrice { get; set; } 

    // YES ?: An image might be missing, so allow NULL in the database
    public string? BigImage { get; set; } 

    // YES ?: A category ID might be missing, allow NULL
    public string? CategoryId { get; set; }
    public FlatCategory? Category { get; set; }
    // If you added these earlier, make sure they are nullable too

}
