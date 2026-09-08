namespace Models.AngularDTOs;

public class CjOrderStatusDto
{
    public string ShipmentOrderId { get; set; } = string.Empty;
    public string? OrderStatus { get; set; }
    public string? TrackNumber { get; set; }
    public string? LogisticName { get; set; }
}
