using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Models;

public class OrderItem
{
    [Key]
    public int Id { get; set; }
    
    public string OrderId { get; set; } = string.Empty;
    [ForeignKey("OrderId")]
    public Order Order { get; set; } = null!;

    public string Sku { get; set; } = string.Empty; // Your DB SKU
    public string Name { get; set; } = string.Empty;
    public string Status { get; set; } = "Ordered";
    public string CjVariantId { get; set; } = string.Empty; // The CJ VID
    public int Quantity { get; set; }
    
    [Column(TypeName = "decimal(18,2)")]
    public decimal PriceAtPurchase { get; set; } // Always lock in the price they paid!
}