namespace Models.AngularDTOs;

public class PaymentIntentDto
{
    public string PaymentIntentId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;
    /// <summary>Server-computed charge amount in dollars, for display only.</summary>
    public decimal Amount { get; set; }
    public decimal Subtotal { get; set; }
    public decimal ShippingCost { get; set; }
    public string Currency { get; set; } = "usd";
}

public class PaymentIntentRequest
{
    public string? LogisticName { get; set; }
    public decimal ShippingCost { get; set; }
}
