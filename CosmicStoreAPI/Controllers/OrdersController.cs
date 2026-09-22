using System.Security.Claims;
using CosmicStoreAPI.Util;
using Data.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Models.AngularDTOs;

namespace CosmicStoreAPI.Controllers;

/// <summary>
/// Customer orders: checkout, history, cancellation, refunds, and tokenized guest self-service.
/// </summary>
public class OrdersController(IOrderService orderService, GuestOrderRateLimiter guestRateLimiter) : BaseController
{
    private readonly IOrderService _orderService = orderService;
    private readonly GuestOrderRateLimiter _guestRateLimiter = guestRateLimiter;
    private const string GuestVerifyFailedMessage =
        "We could not verify this order. Check the confirmation email, ZIP, and try again.";
    private const string GuestLockedMessage =
        "Too many attempts. Please wait 15 minutes and try again.";

    /// <summary>
    /// Creates an order from the checkout payload and verifies Stripe payment.
    /// Guests may check out without an Identity user. CJ fulfillment waits until the wallet can cover it.
    /// </summary>
    [AllowAnonymous]
    [HttpPost("checkout")]
    public async Task<ActionResult<OrderDto>> Checkout(AngularCheckoutRequest customerOrder)
    {
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
    /// Guest self-service: token plus email and ZIP. Does not leak whether the order exists.
    /// </summary>
    [AllowAnonymous]
    [HttpPost("guest/verify")]
    public async Task<ActionResult<OrderDto>> VerifyGuestOrder([FromBody] GuestOrderAccessRequest request)
    {
        var ip = ClientIp();
        if (await _guestRateLimiter.IsBlockedAsync(ip, request?.OrderId))
        {
            return StatusCode(StatusCodes.Status429TooManyRequests, new { message = GuestLockedMessage });
        }

        var order = await _orderService.VerifyGuestAccessAsync(request ?? new GuestOrderAccessRequest());
        if (order is not null)
        {
            return Ok(order);
        }

        if (await _guestRateLimiter.RegisterFailureAsync(ip, request?.OrderId))
        {
            return StatusCode(StatusCodes.Status429TooManyRequests, new { message = GuestLockedMessage });
        }

        return BadRequest(new { message = GuestVerifyFailedMessage });
    }

    /// <summary>Guest cancellation after token + email + ZIP verification.</summary>
    [AllowAnonymous]
    [HttpPost("guest/{orderId}/cancel")]
    public async Task<ActionResult<OrderDto>> CancelGuestOrder(string orderId, [FromBody] GuestOrderAccessRequest request)
    {
        return await RunGuestMutation(orderId, request, access => _orderService.RequestGuestCancellationAsync(access));
    }

    /// <summary>Guest line cancel after token + email + ZIP verification.</summary>
    [AllowAnonymous]
    [HttpPost("guest/{orderId}/items/{itemId:int}/cancel")]
    public async Task<ActionResult<OrderDto>> CancelGuestOrderItem(
        string orderId,
        int itemId,
        [FromBody] GuestOrderAccessRequest request)
    {
        return await RunGuestMutation(orderId, request, access => _orderService.CancelGuestOrderItemAsync(access, itemId));
    }

    /// <summary>Guest per-item refund request after token + email + ZIP verification.</summary>
    [AllowAnonymous]
    [HttpPost("guest/{orderId}/items/{itemId:int}/refund")]
    public async Task<ActionResult<OrderDto>> RequestGuestItemRefund(
        string orderId,
        int itemId,
        [FromBody] GuestRefundRequest request)
    {
        return await RunGuestMutation(orderId, request, access =>
        {
            var refund = access as GuestRefundRequest ?? new GuestRefundRequest
            {
                OrderId = access.OrderId,
                Token = access.Token,
                Email = access.Email,
                ZipCode = access.ZipCode,
                ReturnTrackingNumber = request.ReturnTrackingNumber,
                Reason = request.Reason
            };
            return _orderService.RequestGuestItemRefundAsync(refund, itemId);
        });
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
    /// Returns up to three random products the signed-in user bought in the last three months.
    /// </summary>
    [Authorize]
    [HttpGet("recent-purchases")]
    public async Task<ActionResult<IEnumerable<ProductResponseDto>>> GetRecentPurchases()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var products = await _orderService.GetRecentPurchasesAsync(userId);
        return Ok(products);
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

    /// <summary>
    /// Customer cancellation: checks CJ fulfillment status, cancels an unfulfilled shipment, and refunds Stripe.
    /// </summary>
    [Authorize]
    [HttpPost("{orderId}/cancel")]
    public async Task<ActionResult<OrderDto>> CancelOrder(string orderId)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        try
        {
            var order = await _orderService.RequestCancellationAsync(orderId, userId);
            return order is null ? NotFound(new { message = "Order not found." }) : Ok(order);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Cancels one line if the order still belongs to the signed-in user and CJ has not shipped it.
    /// </summary>
    [Authorize]
    [HttpPost("{orderId}/items/{itemId:int}/cancel")]
    public async Task<ActionResult<OrderDto>> CancelOrderItem(string orderId, int itemId)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        try
        {
            var order = await _orderService.CancelOrderItemAsync(orderId, itemId, userId);
            return order is null ? NotFound(new { message = "Order not found." }) : Ok(order);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Customer per-item refund request. Requires a return tracking number and is limited to 30 days after purchase.
    /// </summary>
    [Authorize]
    [HttpPost("{orderId}/items/{itemId:int}/refund")]
    public async Task<ActionResult<OrderDto>> RequestItemRefund(
        string orderId,
        int itemId,
        [FromBody] RequestRefundRequest? request)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        try
        {
            var order = await _orderService.RequestItemRefundAsync(orderId, itemId, userId, request);
            return order is null ? NotFound(new { message = "Order not found." }) : Ok(order);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    private async Task<ActionResult<OrderDto>> RunGuestMutation(
        string orderId,
        GuestOrderAccessRequest? request,
        Func<GuestOrderAccessRequest, Task<OrderDto?>> action)
    {
        request ??= new GuestOrderAccessRequest();
        if (string.IsNullOrWhiteSpace(request.OrderId))
        {
            request.OrderId = orderId;
        }

        var ip = ClientIp();
        if (await _guestRateLimiter.IsBlockedAsync(ip, request.OrderId))
        {
            return StatusCode(StatusCodes.Status429TooManyRequests, new { message = GuestLockedMessage });
        }

        try
        {
            var order = await action(request);
            if (order is not null)
            {
                return Ok(order);
            }
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }

        if (await _guestRateLimiter.RegisterFailureAsync(ip, request.OrderId))
        {
            return StatusCode(StatusCodes.Status429TooManyRequests, new { message = GuestLockedMessage });
        }

        return BadRequest(new { message = GuestVerifyFailedMessage });
    }

    private string ClientIp() =>
        HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
}
