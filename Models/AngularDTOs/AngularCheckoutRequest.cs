namespace Models.AngularDTOs;

public class AngularCheckoutRequest
{
    // 1. Customer Shipping Info
    public string FullName { get; set; } = string.Empty;
    public string StreetAddress { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string ProvinceOrState { get; set; } = string.Empty;
    public string CountryCode { get; set; } = string.Empty; // e.g., "US"
    
    // 2. Payment Info
    public string StripePaymentMethodId { get; set; } = string.Empty;
    
    // 3. Cart Items (Using YOUR database SKUs or IDs)
    public List<CartItems> Items { get; set; } = new();
}