using Data.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Models.AngularDTOs;

namespace CosmicStoreAPI.Controllers;

[Authorize(Roles = "Admin")]
public class AdminCjController(
    ICJDropshippingService cjService,
    ICjCatalogSyncService catalogSyncService,
    IOrderService orderService) : BaseController
{
    private readonly ICJDropshippingService _cjService = cjService;
    private readonly ICjCatalogSyncService _catalogSyncService = catalogSyncService;
    private readonly IOrderService _orderService = orderService;

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

    [HttpPost("sync/variants")]
    public async Task<ActionResult<object>> SyncAllVariants(CancellationToken cancellationToken)
    {
        var count = await _catalogSyncService.SyncAllVariantsAsync(cancellationToken);
        return Ok(new { mappedVariants = count });
    }

    [HttpPost("sync/variants/{productId}")]
    public async Task<ActionResult<object>> SyncProductVariants(string productId)
    {
        var count = await _catalogSyncService.SyncVariantsForProductAsync(productId);
        return Ok(new { mappedVariants = count });
    }

    [HttpPost("sync/stock")]
    public async Task<ActionResult<object>> SyncStock(CancellationToken cancellationToken)
    {
        var count = await _catalogSyncService.SyncStockAsync(cancellationToken);
        return Ok(new { updatedVariants = count });
    }

    [HttpPost("orders/sync")]
    public async Task<ActionResult<object>> SyncPendingOrders(CancellationToken cancellationToken)
    {
        var count = await _orderService.SyncPendingOrdersAsync(cancellationToken);
        return Ok(new { updatedOrders = count });
    }

    [HttpPost("orders/{orderId}/sync")]
    public async Task<ActionResult<OrderDto>> SyncOrder(string orderId)
    {
        var order = await _orderService.SyncOrderStatusAsync(orderId);
        return order is null ? NotFound() : Ok(order);
    }

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

    [HttpPost("orders/{orderId}/refund")]
    public async Task<ActionResult<OrderDto>> RefundOrder(string orderId)
    {
        var order = await _orderService.RefundOrderAsync(orderId);
        return order is null ? NotFound() : Ok(order);
    }
}
