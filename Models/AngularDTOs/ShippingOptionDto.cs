namespace Models.AngularDTOs;

public class ShippingOptionDto
{
    /// <summary>Value sent back to CJ as logisticName when creating the order.</summary>
    public string LogisticName { get; set; } = string.Empty;

    public string? LogisticAim { get; set; }

    public decimal FreightCost { get; set; }

    /// <summary>Estimated delivery window, e.g. "7-15".</summary>
    public string? DeliveryTime { get; set; }
}
