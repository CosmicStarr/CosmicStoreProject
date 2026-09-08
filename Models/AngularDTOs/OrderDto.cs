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
    public string Country { get; set; } = string.Empty;
    public string PaymentStatus { get; set; } = string.Empty;
    public string LogisticName { get; set; } = string.Empty;
    public decimal ShippingCost { get; set; }
    public string? TrackingNumber { get; set; }
    public DateTime? LastStatusSyncAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public decimal Total { get; set; }
    public List<OrderItemDto> Items { get; set; } = new();
}

public class OrderItemDto
{
    public string Sku { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public decimal PriceAtPurchase { get; set; }
}
