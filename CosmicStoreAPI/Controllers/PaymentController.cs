using System.Security.Claims;
using CosmicStoreAPI.Error;
using CosmicStoreAPI.Util;
using Data.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Models.AngularDTOs;
using Stripe;

namespace CosmicStoreAPI.Controllers;

/// <summary>
/// Stripe PaymentIntents for checkout, webhook status updates, and the public publishable key.
/// </summary>
public class PaymentController : BaseController
{
    private readonly IPaymentService _paymentService;
    private readonly IOrderService _orderService;
    private readonly ILogger<PaymentController> _logger;
    private readonly string _webhookSecret;

    public PaymentController(
        IPaymentService paymentService,
        IOrderService orderService,
        IConfiguration configuration,
        ILogger<PaymentController> logger)
    {
        _paymentService = paymentService;
        _orderService = orderService;
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
        catch (InvalidOperationException ex)
        {
            return BadRequest(new ApiErrorResponse(400, ex.Message));
        }
        catch (StripeException ex)
        {
            _logger.LogError(ex, "Stripe rejected the payment intent for cart {CartId}.", cartId);
            return BadRequest(new ApiErrorResponse(400, "We could not start the payment. Please try again."));
        }
    }

    /// <summary>
    /// Stripe callback: marks the matching order paid, failed, or fully refunded.
    /// </summary>
    [AllowAnonymous]
    [HttpPost("webhook")]
    public async Task<ActionResult> StripeWebhook()
    {
        var jsonData = await new StreamReader(HttpContext.Request.Body).ReadToEndAsync();

        if (string.IsNullOrWhiteSpace(_webhookSecret))
        {
            _logger.LogWarning("Stripe:WebhookSecret is not configured. The webhook cannot verify events.");
            return StatusCode(503, new ApiErrorResponse(503, "Stripe webhook is not configured."));
        }

        var signature = Request.Headers["Stripe-Signature"].ToString();
        if (string.IsNullOrWhiteSpace(signature))
        {
            return BadRequest(new ApiErrorResponse(400, "Invalid Stripe signature."));
        }

        Event stripeEvent;

        try
        {
            // The account's API version can lag the SDK's pinned version; that mismatch
            // must not cause us to drop otherwise-valid events.
            stripeEvent = EventUtility.ConstructEvent(
                jsonData,
                signature,
                _webhookSecret,
                throwOnApiVersionMismatch: false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Rejected a Stripe webhook: {Reason}", ex.Message);
            return BadRequest(new ApiErrorResponse(400, "Invalid Stripe signature."));
        }

        switch (stripeEvent.Type)
        {
            case "payment_intent.succeeded":
                if (stripeEvent.Data.Object is PaymentIntent paidIntent)
                {
                    _logger.LogInformation("Payment succeeded for intent {IntentId}.", paidIntent.Id);
                    var paidOrder = await _paymentService.MarkPaymentSucceededAsync(paidIntent.Id);
                    if (paidOrder is not null)
                    {
                        _logger.LogInformation("Order {OrderId} marked as paid.", paidOrder.OrderId);
                    }
                }
                break;

            case "payment_intent.payment_failed":
                if (stripeEvent.Data.Object is PaymentIntent failedIntent)
                {
                    _logger.LogWarning("Payment failed for intent {IntentId}.", failedIntent.Id);
                    var failedOrder = await _paymentService.MarkPaymentFailedAsync(failedIntent.Id);
                    if (failedOrder is not null)
                    {
                        _logger.LogInformation("Order {OrderId} marked as payment failed.", failedOrder.OrderId);
                    }
                }
                break;

            case "charge.refunded":
                if (stripeEvent.Data.Object is Charge charge
                    && charge.Refunded
                    && !string.IsNullOrWhiteSpace(charge.PaymentIntentId))
                {
                    _logger.LogInformation(
                        "Charge {ChargeId} fully refunded for intent {IntentId}.",
                        charge.Id,
                        charge.PaymentIntentId);
                    var refundedOrder = await _orderService.ApplyPaymentRefundedAsync(charge.PaymentIntentId);
                    if (refundedOrder is not null)
                    {
                        _logger.LogInformation("Order {OrderId} marked as refunded.", refundedOrder.OrderId);
                    }
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
