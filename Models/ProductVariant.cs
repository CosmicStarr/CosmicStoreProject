using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Models;

/// <summary>Maps a storefront product SKU to the CJ variant (vid) required when placing orders.</summary>
public class ProductVariant
{
    [Key]
    public int Id { get; set; }

    public string ProductId { get; set; } = string.Empty;

    [ForeignKey("ProductId")]
    public Products? Product { get; set; }

    /// <summary>CJ variant identifier (vid) sent to the order API.</summary>
    public string CjVariantId { get; set; } = string.Empty;

    /// <summary>CJ variant SKU. Unique per variant.</summary>
    public string Sku { get; set; } = string.Empty;

    public string? VariantName { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal CjPrice { get; set; }

    public int StockQuantity { get; set; }

    public string? ImageUrl { get; set; }

    public DateTime LastSyncedAt { get; set; } = DateTime.UtcNow;
}
