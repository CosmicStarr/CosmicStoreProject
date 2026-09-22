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

    /// <summary>Customer return carrier tracking, used when opening a CJ dispute.</summary>
    public string? ReturnTrackingNumber { get; set; }

    /// <summary>CJ dispute id for a return, when one has been opened.</summary>
    public string? CjDisputeId { get; set; }

    /// <summary>Latest CJ dispute status (Processing, Completed, REFUND_COMPLETE, etc.).</summary>
    public string? CjDisputeStatus { get; set; }

    /// <summary>True when CJ has confirmed the returned merchandise was received.</summary>
    public bool ReturnReceived { get; set; }

    public DateTime? LastStatusSyncAt { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>SHA-256 hex of the guest manage-order token. Never returned to clients.</summary>
    [MaxLength(64)]
    public string? GuestAccessTokenHash { get; set; }

    /// <summary>Guest manage links stay valid through the refund window (or until this timestamp).</summary>
    public DateTime? GuestAccessTokenExpiresAt { get; set; }

    /// <summary>Gift registry this order fulfilled, when the buyer checked out from a public wishlist.</summary>
    public int? WishlistId { get; set; }

    /// <summary>UTC time the customer agreed to Terms & Privacy at checkout.</summary>
    public DateTime? AcceptedTermsAt { get; set; }

    [MaxLength(32)]
    public string? AcceptedTermsVersion { get; set; }

    // Navigation Property
    public ICollection<OrderItem> Items { get; set; } = new List<OrderItem>();
}