using System.Security.Claims;
using CosmicStoreAPI.Util;
using Data.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Models;
using Models.AngularDTOs;

namespace CosmicStoreAPI.Controllers;

/// <summary>
/// Signed-in customer orders: place checkout and list/read own orders.
/// </summary>
public class OrdersController(IOrderService orderService, UserManager<AppUser> userManager) : BaseController
{
    private readonly IOrderService _orderService = orderService;
    private readonly UserManager<AppUser> _userManager = userManager;

    /// <summary>
    /// Creates an order from the checkout payload, verifies Stripe payment, and maps each line SKU to a CJ vid.
    /// Guests may check out without an Identity user. CJ fulfillment submit is currently skipped in OrderService.
    /// </summary>
    [AllowAnonymous]
    [HttpPost("checkout")]
    public async Task<ActionResult<OrderDto>> Checkout(AngularCheckoutRequest customerOrder)
    {
        var blocked = await ConfirmedEmailGate.UnconfirmedMessageAsync(_userManager, User);
        if (blocked is not null) return StatusCode(403, new { message = blocked });

        try
        {
            var isGuest = User.Identity?.IsAuthenticated != true || GuestPrincipal.IsGuest(User);
            var userId = isGuest ? null : User.FindFirstValue(ClaimTypes.NameIdentifier);
            var email = string.IsNullOrWhiteSpace(customerOrder.Email)
                ? User.FindFirstValue(ClaimTypes.Email)
                : customerOrder.Email.Trim();
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

    /// <summary>
    /// Lists the signed-in user's orders, newest first.
    /// </summary>
    [Authorize]
    [HttpGet]
    public async Task<ActionResult<IEnumerable<OrderDto>>> GetOrders()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var orders = await _orderService.GetUserOrdersAsync(userId);
        return Ok(orders);
    }

    /// <summary>
    /// Returns one order if it belongs to the signed-in user.
    /// </summary>
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
