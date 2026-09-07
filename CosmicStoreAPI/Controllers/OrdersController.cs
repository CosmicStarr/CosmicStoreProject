using System.Security.Claims;
using Data.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Models.AngularDTOs;

namespace CosmicStoreAPI.Controllers;

public class OrdersController(IOrderService orderService) : BaseController
{
    private readonly IOrderService _orderService = orderService;

    [Authorize]
    [HttpPost("checkout")]
    public async Task<ActionResult<OrderDto>> Checkout(AngularCheckoutRequest customerOrder)
    {
        try
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var email = User.FindFirstValue(ClaimTypes.Email);
            var order = await _orderService.CreateOrderAsync(customerOrder, userId, email);
            return Ok(order);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = ex.Message });
        }
    }

    [Authorize]
    [HttpGet]
    public async Task<ActionResult<IEnumerable<OrderDto>>> GetOrders()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var orders = await _orderService.GetUserOrdersAsync(userId);
        return Ok(orders);
    }

    [Authorize]
    [HttpGet("{orderId}")]
    public async Task<ActionResult<OrderDto>> GetOrder(string orderId)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var order = await _orderService.GetOrderAsync(orderId, userId);
        if (order is null) return NotFound(new { message = "Order not found." });
        return Ok(order);
    }
}
