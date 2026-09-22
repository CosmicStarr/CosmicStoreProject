using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Models;

/// <summary>Single-row storefront runtime settings editable from Admin Settings.</summary>
public class StoreRuntimeSettings
{
    public int Id { get; set; } = 1;

    [Column(TypeName = "decimal(18,2)")]
    public decimal DefaultMarkup { get; set; } = 2.0m;

    /// <summary>Catalog sync interval in hours. Allowed values: 6 or 12.</summary>
    public int CatalogSyncHours { get; set; } = 6;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
