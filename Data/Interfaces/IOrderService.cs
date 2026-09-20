using Models.AngularDTOs;

namespace Data.Interfaces;

public interface IOrderService
{
    Task<OrderDto> CreateOrderAsync(AngularCheckoutRequest request, string? userId, string? userEmail);
    Task<IEnumerable<OrderDto>> GetUserOrdersAsync(string userId);
    Task<IEnumerable<OrderDto>> GetAllOrdersAsync();
    Task<OrderDto?> GetOrderAsync(string orderId, string? userId = null);

    /// <summary>Retries CJ submit when the wallet can cover it, then refreshes shipment status.</summary>
    Task<OrderDto?> SyncOrderStatusAsync(string orderId);

    /// <summary>
    /// Submits queued Stripe-paid orders when the CJ wallet can cover them, then polls open shipments.
    /// </summary>
    Task<int> SyncPendingOrdersAsync(CancellationToken cancellationToken = default);

    /// <summary>Cancels the order with CJ (when submitted) and marks it cancelled locally.</summary>
    Task<OrderDto?> CancelOrderAsync(string orderId);

    /// <summary>
    /// Customer cancellation: asks CJ whether the shipment is still unfulfilled, then cancels
    /// on CJ and refunds Stripe. Shipped orders are refused.
    /// </summary>
    Task<OrderDto?> RequestCancellationAsync(string orderId, string userId);

    /// <summary>Cancels one line, refunds that line through Stripe, and leaves the rest of the order intact.</summary>
    Task<OrderDto?> CancelOrderItemAsync(string orderId, int itemId);

    /// <summary>Refunds the Stripe PaymentIntent, then marks the order refunded.</summary>
    Task<OrderDto?> RefundOrderAsync(string orderId);

    /// <summary>
    /// Applies a full Stripe refund that already happened (Dashboard or webhook) to the matching order.
    /// </summary>
    Task<OrderDto?> ApplyPaymentRefundedAsync(string paymentIntentId);
}
