namespace Models;

public class CjCreateOrderResponse
{
    public bool result { get; set; }
    public string? message { get; set; }
    public CjOrderData? data { get; set; }
}