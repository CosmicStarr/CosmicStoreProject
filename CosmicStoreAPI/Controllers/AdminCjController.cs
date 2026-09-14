using Data.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Models.AngularDTOs;

namespace CosmicStoreAPI.Controllers;

/// <summary>
/// Admin CJ operations: import preview by pid, wallet, variant/stock sync, and order cancel/refund.
/// </summary>
[Authorize(Roles = "Admin")]
public class AdminCjController(
    ICJDropshippingService cjService,
    ICjCatalogSyncService catalogSyncService,
    IOrderService orderService,
    IConfiguration configuration) : BaseController
{
    private readonly ICJDropshippingService _cjService = cjService;
    private readonly ICjCatalogSyncService _catalogSyncService = catalogSyncService;
    private readonly IOrderService _orderService = orderService;
    private readonly IConfiguration _configuration = configuration;

    /// <summary>
    /// Loads CJ product metadata and variants from a pid, product SKU, or variant SKU.
    /// </summary>
    [HttpPost("preview-product")]
    public async Task<ActionResult<CjProductImportDto>> PreviewProduct([FromBody] CjProductPreviewRequest request)
    {
        var key = FirstNonEmpty(request.Pid, request.Sku);
        if (string.IsNullOrWhiteSpace(key))
        {
            return BadRequest(new { message = "Enter a CJ product id (pid) or SKU." });
        }

        var lookup = await _cjService.LookupProductAsync(key);
        if (lookup is null || lookup.Variants.Count == 0)
        {
            return BadRequest(new { message = "CJ returned no product or variants for that pid or SKU." });
        }

        var details = lookup.Details;
        var variants = lookup.Variants;
        var pid = FirstNonEmpty(lookup.Pid, details?.Pid, key);
        var matchedVariant = variants.FirstOrDefault(variant =>
            string.Equals(variant.Sku, key, StringComparison.OrdinalIgnoreCase));
        var markup = _configuration.GetValue("StoreSettings:DefaultMarkup", 2.0m);
        var cjPrice = matchedVariant?.SellPrice ?? variants[0].SellPrice;

        return Ok(new CjProductImportDto
        {
            CjProductId = pid,
            NameEn = FirstNonEmpty(details?.ProductNameEn, matchedVariant?.VariantName, variants[0].VariantName, pid),
            Sku = FirstNonEmpty(details?.ProductSku, matchedVariant?.Sku, variants[0].Sku, pid),
            DescriptionEn = details?.Description,
            BigImage = FirstNonEmpty(details?.ProductImage, matchedVariant?.ImageUrl, variants[0].ImageUrl),
            Category = details?.CategoryName,
            SellPrice = cjPrice > 0 ? Math.Round(cjPrice * markup, 2) : 0m,
            Variants = variants.ToList()
        });
    }

    /// <summary>
    /// Reads the CJ wallet balance used to pay fulfillment.
    /// </summary>
    [HttpGet("balance")]
    public async Task<ActionResult<CjBalanceDto>> GetBalance()
    {
        var balance = await _cjService.GetWalletBalanceAsync();

        if (balance is null)
        {
            return StatusCode(503, new { message = "CJ wallet balance is unavailable right now." });
        }

        return Ok(balance);
    }

    /// <summary>
    /// Pulls CJ variants for every storefront product whose id is a CJ pid.
    /// </summary>
    [HttpPost("sync/variants")]
    public async Task<ActionResult<object>> SyncAllVariants(CancellationToken cancellationToken)
    {
        var count = await _catalogSyncService.SyncAllVariantsAsync(cancellationToken);
        return Ok(new { mappedVariants = count });
    }

    /// <summary>
    /// Pulls CJ variants for one storefront product (expects Products.Id to be the CJ pid).
    /// </summary>
    [HttpPost("sync/variants/{productId}")]
    public async Task<ActionResult<object>> SyncProductVariants(string productId)
    {
        var count = await _catalogSyncService.SyncVariantsForProductAsync(productId);
        return Ok(new { mappedVariants = count });
    }

    /// <summary>
    /// Refreshes warehouse stock on ProductVariant rows and rolls totals up to Products.
    /// </summary>
    [HttpPost("sync/stock")]
    public async Task<ActionResult<object>> SyncStock(CancellationToken cancellationToken)
    {
        var count = await _catalogSyncService.SyncStockAsync(cancellationToken);
        return Ok(new { updatedVariants = count });
    }

    /// <summary>
    /// Polls CJ for status/tracking on orders that still have a shipment id (currently a no-op while fulfillment is off).
    /// </summary>
    [HttpPost("orders/sync")]
    public async Task<ActionResult<object>> SyncPendingOrders(CancellationToken cancellationToken)
    {
        var count = await _orderService.SyncPendingOrdersAsync(cancellationToken);
        return Ok(new { updatedOrders = count });
    }

    /// <summary>
    /// Refreshes one order's CJ shipment status.
    /// </summary>
    [HttpPost("orders/{orderId}/sync")]
    public async Task<ActionResult<OrderDto>> SyncOrder(string orderId)
    {
        var order = await _orderService.SyncOrderStatusAsync(orderId);
        return order is null ? NotFound() : Ok(order);
    }

    /// <summary>
    /// Requests cancellation of an unshipped CJ order and updates local status.
    /// </summary>
    [HttpPost("orders/{orderId}/cancel")]
    public async Task<ActionResult<OrderDto>> CancelOrder(string orderId)
    {
        try
        {
            var order = await _orderService.CancelOrderAsync(orderId);
            return order is null ? NotFound() : Ok(order);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Marks an order as Refunded in the store database (does not call Stripe or CJ).
    /// </summary>
    [HttpPost("orders/{orderId}/refund")]
    public async Task<ActionResult<OrderDto>> RefundOrder(string orderId)
    {
        var order = await _orderService.RefundOrderAsync(orderId);
        return order is null ? NotFound() : Ok(order);
    }

    /// <summary>First non-blank string among CJ name/sku/image fallbacks.</summary>
    private static string FirstNonEmpty(params string?[] values)
    {
        return values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))?.Trim() ?? string.Empty;
    }
}
