using System.Security.Claims;
using CosmicStoreAPI.Error;
using CosmicStoreAPI.Util;
using Data.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Models;
using Models.AngularDTOs;
using Stripe;

namespace CosmicStoreAPI.Controllers;

/// <summary>
/// Stripe PaymentIntents for checkout, webhook status updates, and the public publishable key.
/// </summary>
public class PaymentController : BaseController
{
    private readonly IPaymentService _paymentService;
    private readonly UserManager<AppUser> _userManager;
    private readonly ILogger<PaymentController> _logger;
    private readonly string _webhookSecret;

    public PaymentController(
        IPaymentService paymentService,
        UserManager<AppUser> userManager,
        IConfiguration configuration,
        ILogger<PaymentController> logger)
    {
        _paymentService = paymentService;
        _userManager = userManager;
        _logger = logger;
        _webhookSecret = configuration["Stripe:WebhookSecret"] ?? string.Empty;
    }

    /// <summary>
    /// Creates or updates a Stripe PaymentIntent for the cart total (items + selected shipping).
    /// </summary>
    [AllowAnonymous]
    [HttpPost("{cartId}")]
    public async Task<ActionResult<PaymentIntentDto>> CreateOrUpdatePaymentIntent(
        string cartId,
        PaymentIntentRequest request)
    {
        var blocked = await ConfirmedEmailGate.UnconfirmedMessageAsync(_userManager, User);
        if (blocked is not null) return StatusCode(403, new { message = blocked });

        var isGuest = User.Identity?.IsAuthenticated != true || GuestPrincipal.IsGuest(User);
        var userId = isGuest ? null : User.FindFirstValue(ClaimTypes.NameIdentifier);

        try
        {
            var payment = await _paymentService.CreateOrUpdatePaymentAsync(cartId, userId, request);

            if (payment is null)
            {
                return BadRequest(new ApiErrorResponse(400, "There has been a problem retrieving your shopping cart!"));
            }

            return Ok(payment);
        }
        catch (StripeException ex)
        {
            _logger.LogError(ex, "Stripe rejected the payment intent for cart {CartId}.", cartId);
            return BadRequest(new ApiErrorResponse(400, "We could not start the payment. Please try again."));
        }
    }

    /// <summary>
    /// Stripe callback: marks the matching order paid or failed from payment_intent events.
    /// </summary>
    [AllowAnonymous]
    [HttpPost("webhook")]
    public async Task<ActionResult> StripeWebhook()
    {
        var jsonData = await new StreamReader(HttpContext.Request.Body).ReadToEndAsync();

        Event stripeEvent;

        try
        {
            // The account's API version can lag the SDK's pinned version; that mismatch
            // must not cause us to drop otherwise-valid events.
            stripeEvent = EventUtility.ConstructEvent(
                jsonData,
                Request.Headers["Stripe-Signature"],
                _webhookSecret,
                throwOnApiVersionMismatch: false);
        }
        catch (StripeException ex)
        {
            _logger.LogWarning(ex, "Rejected a Stripe webhook: {Reason}", ex.Message);
            return BadRequest(new ApiErrorResponse(400, "Invalid Stripe signature."));
        }

        if (stripeEvent.Data.Object is not PaymentIntent intent)
        {
            return Ok();
        }

        switch (stripeEvent.Type)
        {
            case "payment_intent.succeeded":
                _logger.LogInformation("Payment succeeded for intent {IntentId}.", intent.Id);
                var paidOrder = await _paymentService.MarkPaymentSucceededAsync(intent.Id);
                if (paidOrder is not null)
                {
                    _logger.LogInformation("Order {OrderId} marked as paid.", paidOrder.OrderId);
                }
                break;

            case "payment_intent.payment_failed":
                _logger.LogWarning("Payment failed for intent {IntentId}.", intent.Id);
                var failedOrder = await _paymentService.MarkPaymentFailedAsync(intent.Id);
                if (failedOrder is not null)
                {
                    _logger.LogInformation("Order {OrderId} marked as payment failed.", failedOrder.OrderId);
                }
                break;
        }

        return Ok();
    }

    /// <summary>
    /// Returns the Stripe publishable key for the Angular checkout form (no secret).
    /// </summary>
    [HttpGet("config")]
    [AllowAnonymous]
    public ActionResult<object> GetConfig([FromServices] IConfiguration configuration)
    {
        return Ok(new { publishableKey = configuration["Stripe:PublishableKey"] });
    }
}
