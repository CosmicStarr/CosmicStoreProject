using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Models;

public class Order
{
    [Key]
    public string OrderId { get; set; } = Guid.NewGuid().ToString(); // Your internal ID
    
    // CJ Dropshipping ID (Populated AFTER you send to CJ)
    public string? CjShipmentOrderId { get; set; } 
    
    // Status tracking (e.g., "Pending", "Paid", "Shipped", "Delivered")
    public string Status { get; set; } = "Pending";
    
    // Customer Info (Keep this!)
    public string? AppUserId { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public string CustomerEmail { get; set; } = string.Empty;
    public string ShippingAddress { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string State { get; set; } = string.Empty;
    public string ZipCode { get; set; } = string.Empty;
    public string Country { get; set; } = string.Empty;

    // The Stripe PaymentIntent id, used for accounting and webhook reconciliation
    public string PaymentTransactionId { get; set; } = string.Empty;

    public string PaymentStatus { get; set; } = nameof(Models.Status.Pending);

    // Shipping selection and CJ fulfilment tracking
    public string LogisticName { get; set; } = "CJPacket Ordinary";

    [Column(TypeName = "decimal(18,2)")]
    public decimal ShippingCost { get; set; }

    public string? TrackingNumber { get; set; }

    public DateTime? LastStatusSyncAt { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation Property
    public ICollection<OrderItem> Items { get; set; } = new List<OrderItem>();
}