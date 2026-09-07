using System.Text.Json;
using System.Text;
using Data.Interfaces;
using Models;

// ... inside your class ...
namespace Data.Classes;
public class CJDropshippingService : ICJDropshippingService
{
    private readonly HttpClient _httpClient;
    private readonly CjAuthManager _authManager;

    public CJDropshippingService(HttpClient httpClient, CjAuthManager authManager)
    {
        _httpClient = httpClient;
        _authManager = authManager;
    }

    public async Task<string> CreateOrderV3Async(CjCreateOrderV3Request requestPayload)
    {
        // 1. Get the Token
        var token = await _authManager.GetValidAccessTokenAsync(); 
        if (string.IsNullOrWhiteSpace(token)) throw new Exception("Access Token is missing.");

        // 2. Prepare the Request
        _httpClient.DefaultRequestHeaders.Clear();
        _httpClient.DefaultRequestHeaders.TryAddWithoutValidation("CJ-Access-Token", token);
        
        var requestUrl = "https://developers.cjdropshipping.com/api2.0/v1/shopping/order/createOrderV3";
        
        var jsonPayload = JsonSerializer.Serialize(requestPayload);
        var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

        // 3. Execute
        var response = await _httpClient.PostAsync(requestUrl, content);
        var jsonResponse = await response.Content.ReadAsStringAsync();

        // 4. Parse the result to extract the shipmentOrderId
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var apiResult = JsonSerializer.Deserialize<CjCreateOrderResponse>(jsonResponse, options);

        if (apiResult?.result == true && !string.IsNullOrWhiteSpace(apiResult.data?.shipmentOrderId))
        {
            return apiResult.data.shipmentOrderId;
        }

        throw new Exception($"Failed to create order. CJ Response: {jsonResponse}");
    }

    public async Task<bool> PayBalanceV2Async(string shipmentOrderId)
    {
        var token = await _authManager.GetValidAccessTokenAsync();
        if (string.IsNullOrWhiteSpace(token)) throw new Exception("Access Token is missing.");

        _httpClient.DefaultRequestHeaders.Clear();
        _httpClient.DefaultRequestHeaders.TryAddWithoutValidation("CJ-Access-Token", token);
        
        var requestUrl = "https://developers.cjdropshipping.com/api2.0/v1/shopping/pay/payBalanceV2";
        
        // Create the small JSON payload containing the shipment order ID
        var payload = new { shipmentOrderId = shipmentOrderId };
        var jsonPayload = JsonSerializer.Serialize(payload);
        var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

        var response = await _httpClient.PostAsync(requestUrl, content);
        var jsonResponse = await response.Content.ReadAsStringAsync();

        // If it succeeds, the "result" boolean will be true
        using var document = JsonDocument.Parse(jsonResponse);
        return document.RootElement.GetProperty("result").GetBoolean();
    }

}