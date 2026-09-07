using System.ComponentModel.DataAnnotations;

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
    public string Country { get; set; } = string.Empty;

    // The Stripe/PayPal transaction ID for accounting
    public string PaymentTransactionId { get; set; } = string.Empty;
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation Property
    public ICollection<OrderItem> Items { get; set; } = new List<OrderItem>();
}