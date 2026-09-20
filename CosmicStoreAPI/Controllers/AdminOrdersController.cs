using Data.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Models.AngularDTOs;

namespace CosmicStoreAPI.Controllers;

/// <summary>
/// Admin view of every storefront order.
/// </summary>
[Authorize(Roles = "Admin")]
public class AdminOrdersController(IOrderService orderService) : BaseController
{
    private readonly IOrderService _orderService = orderService;

    /// <summary>
    /// Lists all orders in the store, newest first.
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<IEnumerable<OrderDto>>> GetAllOrders()
    {
        var orders = await _orderService.GetAllOrdersAsync();
        return Ok(orders);
    }

    /// <summary>
    /// Returns any order by id (not limited to a customer).
    /// </summary>
    [HttpGet("{orderId}")]
    public async Task<ActionResult<OrderDto>> GetOrder(string orderId)
    {
        var order = await _orderService.GetOrderAsync(orderId);
        if (order is null) return NotFound();
        return Ok(order);
    }

    /// <summary>
    /// Cancels one line item and refunds that line through Stripe. Other items stay on the order.
    /// </summary>
    [HttpPost("{orderId}/items/{itemId:int}/cancel")]
    public async Task<ActionResult<OrderDto>> CancelOrderItem(string orderId, int itemId)
    {
        try
        {
            var order = await _orderService.CancelOrderItemAsync(orderId, itemId);
            return order is null ? NotFound() : Ok(order);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }
}
