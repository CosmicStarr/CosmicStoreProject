using System.Text;
using System.Text.Json;
using Data.Interfaces;
using Data.Util;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Models;
using Models.AngularDTOs;

namespace Data.Classes;

/// <summary>
/// HTTP client for the CJ Dropshipping API (orders, freight, wallet, variants, stock).
/// </summary>
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

    /// <summary>Submits a supplier order and returns the CJ shipment order id.</summary>
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

    /// <summary>Pays a CJ shipment from the wallet.</summary>
    public async Task<bool> PayBalanceV2Async(string shipmentOrderId)
    {
        var jsonResponse = await PostAsync("/v1/shopping/pay/payBalanceV2", new { shipmentOrderId });

        using var document = JsonDocument.Parse(jsonResponse);
        return document.RootElement.TryGetProperty("result", out var result) && result.GetBoolean();
    }

    /// <summary>Quotes CJ logistics options for a destination and a basket of vids.</summary>
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

    /// <summary>Reads the CJ wallet balance and flags it when it is below the configured threshold.</summary>
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

    /// <summary>Reads fulfillment status and tracking for a CJ shipment order.</summary>
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

    /// <summary>Asks CJ to cancel an unshipped shipment order.</summary>
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

    /// <summary>Lists line items CJ will accept on a dispute for this shipment.</summary>
    public async Task<IReadOnlyList<CjDisputeProduct>> GetDisputeProductsAsync(string cjOrderId)
    {
        try
        {
            var jsonResponse = await GetAsync($"/v1/disputes/disputeProducts?orderId={Uri.EscapeDataString(cjOrderId)}");
            using var document = JsonDocument.Parse(jsonResponse);
            var data = GetDataObject(document.RootElement);
            if (data is null)
            {
                return Array.Empty<CjDisputeProduct>();
            }

            return ReadDisputeProducts(data.Value);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to read CJ dispute products for {OrderId}.", cjOrderId);
            return Array.Empty<CjDisputeProduct>();
        }
    }

    /// <summary>Asks CJ which refund reasons apply to the selected dispute lines.</summary>
    public async Task<CjDisputeConfirmInfo?> ConfirmDisputeInfoAsync(string cjOrderId, IReadOnlyList<CjDisputeProduct> products)
    {
        try
        {
            var jsonResponse = await PostAsync("/v1/disputes/disputeConfirmInfo", new
            {
                orderId = cjOrderId,
                productInfoList = products.Select(product => new
                {
                    lineItemId = product.LineItemId,
                    quantity = product.Quantity.ToString(),
                    price = product.Price
                }).ToList()
            });

            using var document = JsonDocument.Parse(jsonResponse);
            var data = GetDataObject(document.RootElement);
            if (data is null)
            {
                return null;
            }

            return new CjDisputeConfirmInfo
            {
                Products = ReadDisputeProducts(data.Value).ToList(),
                Reasons = ReadDisputeReasons(data.Value).ToList()
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to confirm CJ dispute info for {OrderId}.", cjOrderId);
            return null;
        }
    }

    /// <summary>Creates a CJ dispute. Null means CJ accepted it; otherwise the error text.</summary>
    public async Task<string?> CreateDisputeAsync(CjCreateDisputeRequest request)
    {
        try
        {
            var jsonResponse = await PostAsync("/v1/disputes/create", new
            {
                orderId = request.OrderId,
                businessDisputeId = request.BusinessDisputeId,
                disputeReasonId = request.DisputeReasonId,
                expectType = request.ExpectType,
                refundType = request.RefundType,
                messageText = request.MessageText,
                imageUrl = Array.Empty<string>(),
                productInfoList = request.Products.Select(product => new
                {
                    lineItemId = product.LineItemId,
                    quantity = product.Quantity.ToString(),
                    price = product.Price
                }).ToList()
            });

            using var document = JsonDocument.Parse(jsonResponse);
            var root = document.RootElement;
            if (root.TryGetProperty("result", out var result) && result.ValueKind == JsonValueKind.True)
            {
                return null;
            }

            var message = ReadString(root, "message") ?? "CJ did not open the dispute.";
            _logger.LogWarning("CJ create dispute failed: {Response}", jsonResponse);
            return message;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create CJ dispute for {OrderId}.", request.OrderId);
            return "Could not reach CJ to open the dispute.";
        }
    }

    /// <summary>Lists disputes for a CJ shipment id and/or CosmicStore order number.</summary>
    public async Task<IReadOnlyList<CjDisputeDto>> GetDisputesAsync(string? cjOrderId, string? orderNumber = null)
    {
        try
        {
            var query = new List<string> { "pageNum=1", "pageSize=20" };
            if (!string.IsNullOrWhiteSpace(cjOrderId))
            {
                query.Add($"orderId={Uri.EscapeDataString(cjOrderId)}");
            }

            if (!string.IsNullOrWhiteSpace(orderNumber))
            {
                query.Add($"orderNumber={Uri.EscapeDataString(orderNumber)}");
            }

            var jsonResponse = await GetAsync($"/v1/disputes/getDisputeList?{string.Join("&", query)}");
            using var document = JsonDocument.Parse(jsonResponse);
            if (!IsSuccess(document.RootElement) || !document.RootElement.TryGetProperty("data", out var data))
            {
                return Array.Empty<CjDisputeDto>();
            }

            JsonElement list = data;
            if (data.ValueKind == JsonValueKind.Object && data.TryGetProperty("list", out var nested))
            {
                list = nested;
            }

            if (list.ValueKind != JsonValueKind.Array)
            {
                return Array.Empty<CjDisputeDto>();
            }

            return list.EnumerateArray()
                .Select(MapDispute)
                .Where(dispute => !string.IsNullOrWhiteSpace(dispute.DisputeId) || !string.IsNullOrWhiteSpace(dispute.Status))
                .ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to list CJ disputes for {OrderId}.", cjOrderId);
            return Array.Empty<CjDisputeDto>();
        }
    }

    /// <summary>Reads one dispute, including final deal and refund amount.</summary>
    public async Task<CjDisputeDto?> GetDisputeDetailAsync(string disputeId)
    {
        try
        {
            var jsonResponse = await GetAsync($"/v1/disputes/getDisputeDetail?disputeId={Uri.EscapeDataString(disputeId)}");
            using var document = JsonDocument.Parse(jsonResponse);
            var data = GetDataObject(document.RootElement);
            return data is null ? null : MapDispute(data.Value);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to read CJ dispute {DisputeId}.", disputeId);
            return null;
        }
    }

    /// <summary>Lists variants (vid, SKU, price, image) for a CJ pid.</summary>
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

    /// <summary>Reads CJ product name, SKU, image, and description for Add Product preview.</summary>
    public async Task<CjProductDetailsDto?> GetProductByPidAsync(string cjProductId)
    {
        var queried = await QueryProductAsync("pid", cjProductId);
        return queried?.Details;
    }

    /// <summary>
    /// Tries pid, then variantSku, then productSku. Loads all variants for the parent product.
    /// </summary>
    public async Task<CjProductLookup?> LookupProductAsync(string pidOrSku)
    {
        var key = pidOrSku?.Trim();
        if (string.IsNullOrWhiteSpace(key))
        {
            return null;
        }

        foreach (var field in new[] { "pid", "variantSku", "productSku" })
        {
            var queried = await QueryProductAsync(field, key);
            if (queried is null)
            {
                continue;
            }

            var details = queried.Value.Details;
            var variants = queried.Value.Variants;
            var pid = details.Pid?.Trim();

            if (variants.Count == 0 && !string.IsNullOrWhiteSpace(pid))
            {
                variants = (await GetProductVariantsAsync(pid)).ToList();
            }

            if (string.IsNullOrWhiteSpace(pid) && variants.Count == 0)
            {
                continue;
            }

            return new CjProductLookup
            {
                Pid = pid ?? string.Empty,
                LookupKind = field,
                Details = details,
                Variants = variants
            };
        }

        return null;
    }

    /// <summary>Matches a SKU to a variant vid after looking the product up on CJ.</summary>
    public async Task<string?> ResolveVidBySkuAsync(string sku)
    {
        var key = sku?.Trim();
        if (string.IsNullOrWhiteSpace(key))
        {
            return null;
        }

        var lookup = await LookupProductAsync(key);
        if (lookup is null)
        {
            return null;
        }

        var match = lookup.Variants.FirstOrDefault(variant =>
            string.Equals(variant.Sku, key, StringComparison.OrdinalIgnoreCase));

        if (!string.IsNullOrWhiteSpace(match?.Vid))
        {
            return match.Vid;
        }

        return lookup.Variants.Count == 1 ? lookup.Variants[0].Vid : null;
    }

    /// <summary>Reads warehouse stock for each vid (paced to stay under CJ rate limits).</summary>
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

    /// <summary>Queries CJ product/query by one of pid, variantSku, or productSku.</summary>
    private async Task<(CjProductDetailsDto Details, List<CjVariantDto> Variants)?> QueryProductAsync(string field, string value)
    {
        try
        {
            var jsonResponse = await GetAsync($"/v1/product/query?{field}={Uri.EscapeDataString(value)}");
            using var document = JsonDocument.Parse(jsonResponse);
            var root = document.RootElement;

            if (!root.TryGetProperty("result", out var result) || result.ValueKind != JsonValueKind.True)
            {
                return null;
            }

            if (!root.TryGetProperty("data", out var data)
                || data.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
            {
                return null;
            }

            var obj = data.ValueKind == JsonValueKind.Array
                ? data.EnumerateArray().FirstOrDefault()
                : data;

            if (obj.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            var details = new CjProductDetailsDto
            {
                Pid = ReadString(obj, "pid"),
                ProductNameEn = ReadString(obj, "productNameEn") ?? ReadString(obj, "productName"),
                ProductSku = ReadString(obj, "productSku"),
                // CJ often sends productImage as a JSON array — keep only a single http(s) URL.
                ProductImage = ImageUrlNormalizer.FromJson(obj, "productImage")
                    ?? ImageUrlNormalizer.FromJson(obj, "bigImage"),
                Description = ReadString(obj, "description") ?? ReadString(obj, "descriptionEn"),
                CategoryName = ReadString(obj, "categoryName")
            };

            return (details, ReadVariants(obj));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to query CJ product by {Field} {Value}.", field, value);
            return null;
        }
    }

    /// <summary>Reads the variants array from a CJ product/query payload, if present.</summary>
    private static List<CjVariantDto> ReadVariants(JsonElement product)
    {
        if (!product.TryGetProperty("variants", out var variants)
            || variants.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        var list = new List<CjVariantDto>();
        foreach (var variant in variants.EnumerateArray())
        {
            var vid = ReadString(variant, "vid");
            if (string.IsNullOrWhiteSpace(vid))
            {
                continue;
            }

            list.Add(new CjVariantDto
            {
                Vid = vid,
                Sku = ReadString(variant, "variantSku") ?? vid,
                VariantName = ReadString(variant, "variantNameEn") ?? ReadString(variant, "variantKey"),
                SellPrice = ReadDecimal(variant, "variantSellPrice"),
                ImageUrl = ImageUrlNormalizer.FromJson(variant, "variantImage")
            });
        }

        return list;
    }

    /// <summary>Reads a JSON number or numeric string as a decimal.</summary>
    private static decimal ReadDecimal(JsonElement obj, string name)
    {
        if (!obj.TryGetProperty(name, out var property))
        {
            return 0m;
        }

        return property.ValueKind switch
        {
            JsonValueKind.Number when property.TryGetDecimal(out var value) => value,
            JsonValueKind.String when decimal.TryParse(property.GetString(), out var parsed) => parsed,
            _ => 0m
        };
    }

    /// <summary>Authenticated GET against the CJ API.</summary>
    private async Task<string> GetAsync(string relativeUrl)
    {
        await ApplyAuthHeaderAsync();
        var response = await _httpClient.GetAsync($"{_baseUrl}{relativeUrl}");
        return await response.Content.ReadAsStringAsync();
    }

    /// <summary>Authenticated POST against the CJ API.</summary>
    private async Task<string> PostAsync(string relativeUrl, object payload)
    {
        await ApplyAuthHeaderAsync();
        var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
        var response = await _httpClient.PostAsync($"{_baseUrl}{relativeUrl}", content);
        return await response.Content.ReadAsStringAsync();
    }

    /// <summary>Attaches a fresh CJ-Access-Token from CjAuthManager.</summary>
    private async Task ApplyAuthHeaderAsync()
    {
        var token = await _authManager.GetValidAccessTokenAsync();
        if (string.IsNullOrWhiteSpace(token)) throw new Exception("Access Token is missing.");

        _httpClient.DefaultRequestHeaders.Clear();
        _httpClient.DefaultRequestHeaders.TryAddWithoutValidation("CJ-Access-Token", token);
    }

    /// <summary>Parses the first number from a CJ price string that may be a range (e.g. "1.2-3.4").</summary>
    private static decimal ParsePrice(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return 0m;

        var firstPart = value.Split(['-', '~'], StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
        return decimal.TryParse(firstPart?.Trim(), out var price) ? price : 0m;
    }

    /// <summary>Reads a JSON property as a string regardless of whether CJ sent a string or number.</summary>
    private static string? ReadString(JsonElement obj, string name)
    {
        if (!obj.TryGetProperty(name, out var property))
            return null;

        return property.ValueKind switch
        {
            JsonValueKind.String => property.GetString(),
            JsonValueKind.Null or JsonValueKind.Undefined => null,
            _ => property.ToString()
        };
    }

    private static bool IsSuccess(JsonElement root) =>
        root.TryGetProperty("result", out var result) && result.ValueKind == JsonValueKind.True;

    private static JsonElement? GetDataObject(JsonElement root)
    {
        if (!IsSuccess(root) || !root.TryGetProperty("data", out var data))
        {
            return null;
        }

        return data.ValueKind == JsonValueKind.Object ? data : null;
    }

    private static IReadOnlyList<CjDisputeProduct> ReadDisputeProducts(JsonElement data)
    {
        if (!data.TryGetProperty("productInfoList", out var list) || list.ValueKind != JsonValueKind.Array)
        {
            return Array.Empty<CjDisputeProduct>();
        }

        return list.EnumerateArray().Select(item => new CjDisputeProduct
        {
            LineItemId = ReadString(item, "lineItemId"),
            Sku = ReadString(item, "sku"),
            Quantity = (int)ReadDecimal(item, "quantity"),
            Price = ReadDecimal(item, "price"),
            CanChoose = item.TryGetProperty("canChoose", out var canChoose)
                && canChoose.ValueKind is JsonValueKind.True or JsonValueKind.False
                && canChoose.GetBoolean()
        }).ToList();
    }

    private static IReadOnlyList<CjDisputeReason> ReadDisputeReasons(JsonElement data)
    {
        if (!data.TryGetProperty("disputeReasonList", out var list) || list.ValueKind != JsonValueKind.Array)
        {
            return Array.Empty<CjDisputeReason>();
        }

        return list.EnumerateArray().Select(item => new CjDisputeReason
        {
            DisputeReasonId = (int)ReadDecimal(item, "disputeReasonId"),
            ReasonName = ReadString(item, "reasonName")
        }).ToList();
    }

    private static CjDisputeDto MapDispute(JsonElement item)
    {
        int? finallyDeal = null;
        if (item.TryGetProperty("finallyDeal", out var deal)
            && deal.ValueKind is not JsonValueKind.Null and not JsonValueKind.Undefined)
        {
            if (deal.ValueKind == JsonValueKind.Number && deal.TryGetInt32(out var numberDeal))
            {
                finallyDeal = numberDeal;
            }
            else if (int.TryParse(deal.ToString(), out var parsedDeal))
            {
                finallyDeal = parsedDeal;
            }
        }

        var status = ReadString(item, "status");
        var money = ReadDecimal(item, "money");
        if (money == 0m)
        {
            money = ReadDecimal(item, "refundAmount");
        }

        return new CjDisputeDto
        {
            DisputeId = ReadString(item, "id") ?? ReadString(item, "disputeId"),
            Status = status,
            DisputeReason = ReadString(item, "disputeReason"),
            Money = money,
            FinallyDeal = finallyDeal,
            FinallyDealLabel = finallyDeal switch
            {
                1 => "Refund",
                2 => "Reissue",
                3 => "Rejected",
                _ => null
            },
            ReturnReceived = IsReturnReceived(status, finallyDeal, money),
            CreateDate = ReadString(item, "createDate")
        };
    }

    private static bool IsReturnReceived(string? status, int? finallyDeal, decimal money)
    {
        if (finallyDeal is 2 or 3)
        {
            return false;
        }

        if (finallyDeal == 1)
        {
            return true;
        }

        var normalized = (status ?? string.Empty)
            .Replace(" ", string.Empty)
            .Replace("_", string.Empty)
            .Replace("-", string.Empty)
            .ToUpperInvariant();

        if (normalized is "REFUNDCOMPLETE" or "RETURNRECEIVED" or "WAREHOUSERECEIVED" or "RECEIVED" or "RETURNED")
        {
            return true;
        }

        return normalized is "COMPLETED" or "COMPLETE" or "CLOSED" && money > 0m;
    }
}
