using Models.AngularDTOs;

namespace Data.Interfaces;

public interface IOrderService
{
    Task<OrderDto> CreateOrderAsync(AngularCheckoutRequest request, string? userId, string? userEmail);
    Task<IEnumerable<OrderDto>> GetUserOrdersAsync(string userId);
    Task<IEnumerable<OrderDto>> GetAllOrdersAsync();
    Task<OrderDto?> GetOrderAsync(string orderId, string? userId = null);

    /// <summary>Deletes store orders older than three months.</summary>
    Task<int> PurgeExpiredOrdersAsync();

    /// <summary>Up to three random catalog products this customer bought in the last three months.</summary>
    Task<IReadOnlyList<ProductResponseDto>> GetRecentPurchasesAsync(string userId);

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
    Task<OrderDto?> RequestCancellationAsync(string orderId, string? userId = null);

    /// <summary>
    /// Cancels one line, refunds that line through Stripe, and leaves the rest of the order intact.
    /// When userId is set, the order must belong to that customer.
    /// </summary>
    Task<OrderDto?> CancelOrderItemAsync(string orderId, int itemId, string? userId = null);

    /// <summary>
    /// Customer per-item refund request with return tracking, allowed for 30 days after purchase.
    /// </summary>
    Task<OrderDto?> RequestItemRefundAsync(string orderId, int itemId, string? userId, RequestRefundRequest? request);

    /// <summary>Validates a guest manage token plus email and ZIP. Returns null on any mismatch.</summary>
    Task<OrderDto?> VerifyGuestAccessAsync(GuestOrderAccessRequest request);

    /// <summary>Guest cancellation after token + email + ZIP verification.</summary>
    Task<OrderDto?> RequestGuestCancellationAsync(GuestOrderAccessRequest request);

    /// <summary>Guest line cancel after token + email + ZIP verification.</summary>
    Task<OrderDto?> CancelGuestOrderItemAsync(GuestOrderAccessRequest request, int itemId);

    /// <summary>Guest per-item refund request after token + email + ZIP verification.</summary>
    Task<OrderDto?> RequestGuestItemRefundAsync(GuestRefundRequest request, int itemId);

    /// <summary>Refunds the Stripe PaymentIntent, then marks the order refunded.</summary>
    Task<OrderDto?> RefundOrderAsync(string orderId);

    /// <summary>
    /// Opens a CJ return dispute for a shipped order using the customer return tracking number.
    /// </summary>
    Task<OrderDto?> OpenReturnDisputeAsync(string orderId, OpenReturnDisputeRequest request);

    /// <summary>
    /// Reloads the CJ dispute and marks the order refund-qualified only after CJ confirms receipt.
    /// </summary>
    Task<OrderDto?> RefreshReturnDisputeAsync(string orderId);

    /// <summary>
    /// Applies a full Stripe refund that already happened (Dashboard or webhook) to the matching order.
    /// </summary>
    Task<OrderDto?> ApplyPaymentRefundedAsync(string paymentIntentId);
}
