namespace Models;

public class CjCreateOrderV3Request
{
    public string orderNumber { get; set; } = string.Empty; // Your system's unique order ID
    public string shippingCountryCode { get; set; } = string.Empty; // e.g., "US"
    public string shippingCountry { get; set; } = string.Empty; // e.g., "United States"
    public string shippingProvince { get; set; } = string.Empty;
    public string shippingCity { get; set; } = string.Empty;
    public string shippingCustomerName { get; set; } = string.Empty;
    public string shippingAddress { get; set; } = string.Empty;
    public string logisticName { get; set; } = string.Empty; // e.g., "CJPacket Ordinary"
    public string fromCountryCode { get; set; } = "CN"; // Usually shipped from China
    public List<CjOrderProduct> products { get; set; } = new();
}