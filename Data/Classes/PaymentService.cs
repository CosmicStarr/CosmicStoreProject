using Data.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Models;
using Models.AngularDTOs;
using Stripe;

namespace Data.Classes;

/// <summary>
/// Stripe PaymentIntents: prices cart lines from the database, then creates or updates the intent.
/// </summary>
public class PaymentService : IPaymentService
{
    private const string Usd = "usd";

    private readonly IShoppingCartService _shoppingCartService;
    private readonly IStoreUnitOfWork _storeUnitOfWork;
    private readonly ILogger<PaymentService> _logger;

    public PaymentService(
        IShoppingCartService shoppingCartService,
        IStoreUnitOfWork storeUnitOfWork,
        IConfiguration configuration,
        ILogger<PaymentService> logger)
    {
        _shoppingCartService = shoppingCartService;
        _storeUnitOfWork = storeUnitOfWork;
        _logger = logger;

        StripeConfiguration.ApiKey = configuration["Stripe:SecretKey"]
            ?? throw new InvalidOperationException("Stripe:SecretKey is not configured.");
    }

    /// <summary>
    /// Re-prices the cart, applies shipping, and creates or updates a Stripe PaymentIntent for that total.
    /// </summary>
    public async Task<PaymentIntentDto?> CreateOrUpdatePaymentAsync(string cartId, string? userId, PaymentIntentRequest request)
    {
        var cart = await _shoppingCartService.GetShoppingCartAsync(cartId, userId);
        if (cart is null || cart.ShoppingCartItems.Count == 0) return null;

        // Re-price every line against the database so a tampered client price can never reach Stripe.
        foreach (var item in cart.ShoppingCartItems)
        {
            var product = await FindProductByLineSkuAsync(item.Sku);

            if (product is null)
            {
                _logger.LogWarning("Cart {CartId} references unknown SKU {Sku}.", cartId, item.Sku);
                return null;
            }

            item.price = product.SellPrice;
            item.Name = product.NameEn;
        }

        cart.LogisticName = request.LogisticName ?? cart.LogisticName;
        cart.ShippingCost = request.ShippingCost;

        var subtotal = cart.ShoppingCartItems.Sum(i => i.Amount * i.price);
        var total = subtotal + cart.ShippingCost;
        var amountInCents = ToMinorUnits(total);

        var intentService = new PaymentIntentService();
        PaymentIntent intent;

        // An intent that already settled (or was cancelled) can't be re-amounted, so start a
        // fresh one. This happens whenever a customer shops again on the same cart.
        if (!string.IsNullOrEmpty(cart.PaymentId) && await IsReusableAsync(cart.PaymentId))
        {
            intent = await intentService.UpdateAsync(cart.PaymentId, new PaymentIntentUpdateOptions
            {
                Amount = amountInCents
            });

            cart.ClientSecret = intent.ClientSecret ?? cart.ClientSecret;
        }
        else
        {
            intent = await intentService.CreateAsync(new PaymentIntentCreateOptions
            {
                Amount = amountInCents,
                Currency = Usd,
                PaymentMethodTypes = ["card"]
            });

            cart.PaymentId = intent.Id;
            cart.ClientSecret = intent.ClientSecret;
        }

        await _shoppingCartService.UpdateShoppingCartAsync(cart, userId);

        return new PaymentIntentDto
        {
            PaymentIntentId = cart.PaymentId,
            ClientSecret = cart.ClientSecret,
            Amount = total,
            Subtotal = subtotal,
            ShippingCost = cart.ShippingCost,
            Currency = Usd
        };
    }

    /// <summary>True when the intent still exists and is awaiting payment, so its amount can be revised.</summary>
    private async Task<bool> IsReusableAsync(string paymentIntentId)
    {
        var existing = await GetPaymentIntentAsync(paymentIntentId);

        return existing?.Status is "requires_payment_method"
            or "requires_confirmation"
            or "requires_action";
    }

    /// <summary>Fetches a PaymentIntent from Stripe, or null if Stripe does not recognize the id.</summary>
    public async Task<PaymentIntent?> GetPaymentIntentAsync(string paymentIntentId)
    {
        try
        {
            return await new PaymentIntentService().GetAsync(paymentIntentId);
        }
        catch (StripeException ex)
        {
            _logger.LogWarning(ex, "Could not retrieve PaymentIntent {PaymentIntentId}.", paymentIntentId);
            return null;
        }
    }

    /// <summary>Webhook helper: marks the matching order as payment received.</summary>
    public Task<Order?> MarkPaymentSucceededAsync(string paymentIntentId) =>
        UpdatePaymentStatusAsync(paymentIntentId, nameof(Status.PaymentRecevied));

    /// <summary>Webhook helper: marks the matching order as payment failed.</summary>
    public Task<Order?> MarkPaymentFailedAsync(string paymentIntentId) =>
        UpdatePaymentStatusAsync(paymentIntentId, nameof(Status.PaymentFailed));

    /// <summary>Updates Order.PaymentStatus (and Status when the charge failed) for a Stripe intent id.</summary>
    private async Task<Order?> UpdatePaymentStatusAsync(string paymentIntentId, string paymentStatus)
    {
        var order = await _storeUnitOfWork.Repository<Order>()
            .GetFirstOrDefault(o => o.PaymentTransactionId == paymentIntentId, includeProperties: "Items");

        if (order is null)
        {
            _logger.LogInformation("No order found for PaymentIntent {PaymentIntentId} yet.", paymentIntentId);
            return null;
        }

        if (order.PaymentStatus == paymentStatus) return order;

        order.PaymentStatus = paymentStatus;

        if (paymentStatus == nameof(Status.PaymentFailed))
        {
            order.Status = Status.PaymentFailed.ToString();
        }

        _storeUnitOfWork.Repository<Order>().Update(order);
        await _storeUnitOfWork.Complete();

        return order;
    }

    /// <summary>Stripe charges in the smallest currency unit, so dollars become integer cents.</summary>
    internal static long ToMinorUnits(decimal amount) =>
        (long)Math.Round(amount * 100m, MidpointRounding.AwayFromZero);

    /// <summary>Finds a storefront product by parent SKU, picture skuPhoto, or ProductVariant SKU.</summary>
    private async Task<Products?> FindProductByLineSkuAsync(string sku)
    {
        var product = await _storeUnitOfWork.Repository<Products>()
            .GetFirstOrDefault(p => p.Sku == sku);
        if (product is not null) return product;

        var image = await _storeUnitOfWork.Repository<ProductImage>()
            .GetFirstOrDefault(img => img.SkuPhoto == sku);
        if (image is not null)
        {
            return await _storeUnitOfWork.Repository<Products>()
                .GetFirstOrDefault(p => p.Id == image.ProductId);
        }

        var variant = await _storeUnitOfWork.Repository<ProductVariant>()
            .GetFirstOrDefault(v => v.Sku == sku);
        if (variant is not null)
        {
            return await _storeUnitOfWork.Repository<Products>()
                .GetFirstOrDefault(p => p.Id == variant.ProductId);
        }

        return null;
    }
}
