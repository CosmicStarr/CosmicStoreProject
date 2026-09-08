using System.Text;
using System.Text.Json;
using Data.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Models;
using Models.AngularDTOs;

namespace Data.Classes;

public class CJDropshippingService : ICJDropshippingService
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    private readonly HttpClient _httpClient;
    private readonly CjAuthManager _authManager;
    private readonly IConfiguration _configuration;
    private readonly ILogger<CJDropshippingService> _logger;
    private readonly string _baseUrl;

    public CJDropshippingService(
        HttpClient httpClient,
        CjAuthManager authManager,
        IOptions<CjAuthRequest> options,
        IConfiguration configuration,
        ILogger<CJDropshippingService> logger)
    {
        _httpClient = httpClient;
        _authManager = authManager;
        _configuration = configuration;
        _logger = logger;
        _baseUrl = (options.Value.BaseUrl ?? "https://developers.cjdropshipping.com/api2.0").TrimEnd('/');
    }

    public async Task<string> CreateOrderV3Async(CjCreateOrderV3Request requestPayload)
    {
        var jsonResponse = await PostAsync("/v1/shopping/order/createOrderV3", requestPayload);
        var apiResult = JsonSerializer.Deserialize<CjCreateOrderResponse>(jsonResponse, JsonOptions);

        if (apiResult?.result == true && !string.IsNullOrWhiteSpace(apiResult.data?.shipmentOrderId))
        {
            return apiResult.data.shipmentOrderId;
        }

        throw new Exception($"Failed to create order. CJ Response: {jsonResponse}");
    }

    public async Task<bool> PayBalanceV2Async(string shipmentOrderId)
    {
        var jsonResponse = await PostAsync("/v1/shopping/pay/payBalanceV2", new { shipmentOrderId });

        using var document = JsonDocument.Parse(jsonResponse);
        return document.RootElement.TryGetProperty("result", out var result) && result.GetBoolean();
    }

    public async Task<IReadOnlyList<ShippingOptionDto>> GetFreightOptionsAsync(CjFreightRequest request)
    {
        try
        {
            var jsonResponse = await PostAsync("/v1/logistic/freightCalculate", request);
            var apiResult = JsonSerializer.Deserialize<CjFreightResponse>(jsonResponse, JsonOptions);

            if (apiResult?.result != true || apiResult.data is null)
            {
                _logger.LogWarning("CJ freight quote returned no options: {Message}", apiResult?.message);
                return Array.Empty<ShippingOptionDto>();
            }

            return apiResult.data
                .Where(option => !string.IsNullOrWhiteSpace(option.logisticName))
                .Select(option => new ShippingOptionDto
                {
                    LogisticName = option.logisticName!,
                    LogisticAim = option.logisticAging,
                    DeliveryTime = option.logisticAging,
                    FreightCost = ParsePrice(option.logisticPrice)
                })
                .OrderBy(option => option.FreightCost)
                .ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch CJ freight options.");
            return Array.Empty<ShippingOptionDto>();
        }
    }

    public async Task<CjBalanceDto?> GetWalletBalanceAsync()
    {
        try
        {
            var jsonResponse = await GetAsync("/v1/shopping/pay/getBalance");
            var apiResult = JsonSerializer.Deserialize<CjBalanceResponse>(jsonResponse, JsonOptions);

            if (apiResult?.result != true || apiResult.data is null)
            {
                return null;
            }

            var threshold = _configuration.GetValue("CJDropshipping:LowBalanceThreshold", 50m);

            return new CjBalanceDto
            {
                Amount = apiResult.data.amount,
                Currency = string.IsNullOrWhiteSpace(apiResult.data.currency) ? "USD" : apiResult.data.currency!,
                Threshold = threshold,
                IsLow = apiResult.data.amount < threshold
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to read CJ wallet balance.");
            return null;
        }
    }

    public async Task<CjOrderStatusDto?> GetOrderStatusAsync(string shipmentOrderId)
    {
        try
        {
            var jsonResponse = await GetAsync($"/v1/shopping/order/getOrderDetail?orderId={Uri.EscapeDataString(shipmentOrderId)}");
            var apiResult = JsonSerializer.Deserialize<CjOrderQueryResponse>(jsonResponse, JsonOptions);

            if (apiResult?.result != true || apiResult.data is null)
            {
                return null;
            }

            return new CjOrderStatusDto
            {
                ShipmentOrderId = shipmentOrderId,
                OrderStatus = apiResult.data.orderStatus,
                TrackNumber = apiResult.data.trackNumber,
                LogisticName = apiResult.data.logisticName
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to read CJ order status for {ShipmentOrderId}.", shipmentOrderId);
            return null;
        }
    }

    public async Task<bool> CancelOrderAsync(string shipmentOrderId)
    {
        try
        {
            var jsonResponse = await PostAsync("/v1/shopping/order/deleteOrder", new { orderId = shipmentOrderId });
            using var document = JsonDocument.Parse(jsonResponse);
            return document.RootElement.TryGetProperty("result", out var result) && result.GetBoolean();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to cancel CJ order {ShipmentOrderId}.", shipmentOrderId);
            return false;
        }
    }

    public async Task<IReadOnlyList<CjVariantDto>> GetProductVariantsAsync(string cjProductId)
    {
        try
        {
            var jsonResponse = await GetAsync($"/v1/product/variant/query?pid={Uri.EscapeDataString(cjProductId)}");
            var apiResult = JsonSerializer.Deserialize<CjVariantQueryResponse>(jsonResponse, JsonOptions);

            if (apiResult?.result != true || apiResult.data is null)
            {
                return Array.Empty<CjVariantDto>();
            }

            return apiResult.data
                .Where(variant => !string.IsNullOrWhiteSpace(variant.vid))
                .Select(variant => new CjVariantDto
                {
                    Vid = variant.vid!,
                    Sku = variant.variantSku ?? variant.vid!,
                    VariantName = variant.variantNameEn,
                    SellPrice = variant.variantSellPrice,
                    ImageUrl = variant.variantImage
                })
                .ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to read CJ variants for product {ProductId}.", cjProductId);
            return Array.Empty<CjVariantDto>();
        }
    }

    public async Task<IReadOnlyDictionary<string, int>> GetVariantStockAsync(IEnumerable<string> variantIds)
    {
        var stock = new Dictionary<string, int>();

        foreach (var vid in variantIds.Distinct().Where(v => !string.IsNullOrWhiteSpace(v)))
        {
            try
            {
                var jsonResponse = await GetAsync($"/v1/product/stock/queryByVid?vid={Uri.EscapeDataString(vid)}");
                var apiResult = JsonSerializer.Deserialize<CjStockResponse>(jsonResponse, JsonOptions);

                if (apiResult?.result == true && apiResult.data is not null)
                {
                    stock[vid] = apiResult.data.Sum(entry => entry.storageNum);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to read CJ stock for variant {Vid}.", vid);
            }

            // CJ throttles aggressively; pace the calls.
            await Task.Delay(400);
        }

        return stock;
    }

    private async Task<string> GetAsync(string relativeUrl)
    {
        await ApplyAuthHeaderAsync();
        var response = await _httpClient.GetAsync($"{_baseUrl}{relativeUrl}");
        return await response.Content.ReadAsStringAsync();
    }

    private async Task<string> PostAsync(string relativeUrl, object payload)
    {
        await ApplyAuthHeaderAsync();
        var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
        var response = await _httpClient.PostAsync($"{_baseUrl}{relativeUrl}", content);
        return await response.Content.ReadAsStringAsync();
    }

    private async Task ApplyAuthHeaderAsync()
    {
        var token = await _authManager.GetValidAccessTokenAsync();
        if (string.IsNullOrWhiteSpace(token)) throw new Exception("Access Token is missing.");

        _httpClient.DefaultRequestHeaders.Clear();
        _httpClient.DefaultRequestHeaders.TryAddWithoutValidation("CJ-Access-Token", token);
    }

    private static decimal ParsePrice(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return 0m;

        var firstPart = value.Split(['-', '~'], StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
        return decimal.TryParse(firstPart?.Trim(), out var price) ? price : 0m;
    }
}
