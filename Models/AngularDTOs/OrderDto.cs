namespace Models.AngularDTOs;

public class OrderDto
{
    public string OrderId { get; set; } = string.Empty;
    public string? CjShipmentOrderId { get; set; }
    public string Status { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public string CustomerEmail { get; set; } = string.Empty;
    public string ShippingAddress { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string State { get; set; } = string.Empty;
    public string ZipCode { get; set; } = string.Empty;
    public string Country { get; set; } = string.Empty;
    public string PaymentStatus { get; set; } = string.Empty;
    public string LogisticName { get; set; } = string.Empty;
    public decimal ShippingCost { get; set; }
    public string? TrackingNumber { get; set; }
    public string? ReturnTrackingNumber { get; set; }
    public string? CjDisputeId { get; set; }
    public string? CjDisputeStatus { get; set; }
    public bool ReturnReceived { get; set; }
    public bool RequiresReturnReceipt { get; set; }
    public CjDisputeDto? Dispute { get; set; }
    public DateTime? LastStatusSyncAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime RefundWindowEndsAt { get; set; }
    public bool IsWithinRefundWindow { get; set; }
    public bool HasRefundRequest { get; set; }
    public decimal Total { get; set; }
    /// <summary>One-time guest manage URL returned only at checkout. Not stored in plaintext.</summary>
    public string? GuestManageUrl { get; set; }

    public int? WishlistId { get; set; }

    /// <summary>Buyer-facing destination when this order fulfilled a gift registry. Never contains street, city, or ZIP.</summary>
    public string? MaskedShippingLabel { get; set; }

    public DateTime? AcceptedTermsAt { get; set; }
    public string? AcceptedTermsVersion { get; set; }

    /// <summary>
    /// Paid but not yet shipped/delivered/cancelled for 20+ days (FTC 30-day ship rule buffer).
    /// </summary>
    public bool NeedsFtcShipAttention { get; set; }

    /// <summary>Whole days since CreatedAt while still awaiting fulfillment.</summary>
    public int DaysAwaitingFulfillment { get; set; }

    public List<OrderItemDto> Items { get; set; } = new();
}

public class GuestOrderAccessRequest
{
    public string OrderId { get; set; } = string.Empty;
    public string Token { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string ZipCode { get; set; } = string.Empty;
}

public class GuestRefundRequest : GuestOrderAccessRequest
{
    public string ReturnTrackingNumber { get; set; } = string.Empty;
    public string? Reason { get; set; }
}

public class OrderItemDto
{
    public int Id { get; set; }
    public string Sku { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public decimal PriceAtPurchase { get; set; }
    public DateTime? RefundRequestedAt { get; set; }
    public string? RefundRequestReason { get; set; }
    public string? ReturnTrackingNumber { get; set; }
    public bool CanRequestRefund { get; set; }
    public bool CanUpdateRefundRequest { get; set; }
}

public class RequestRefundRequest
{
    public string ReturnTrackingNumber { get; set; } = string.Empty;
    public string? Reason { get; set; }
}
