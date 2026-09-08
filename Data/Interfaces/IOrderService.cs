using Models.AngularDTOs;

namespace Data.Interfaces;

public interface IOrderService
{
    Task<OrderDto> CreateOrderAsync(AngularCheckoutRequest request, string? userId, string? userEmail);
    Task<IEnumerable<OrderDto>> GetUserOrdersAsync(string userId);
    Task<IEnumerable<OrderDto>> GetAllOrdersAsync();
    Task<OrderDto?> GetOrderAsync(string orderId, string? userId = null);

    /// <summary>Pulls the latest fulfilment status and tracking number from CJ for a single order.</summary>
    Task<OrderDto?> SyncOrderStatusAsync(string orderId);

    /// <summary>Syncs every order that is not yet in a terminal state. Returns the number updated.</summary>
    Task<int> SyncPendingOrdersAsync(CancellationToken cancellationToken = default);

    /// <summary>Cancels the order with CJ (when submitted) and marks it cancelled locally.</summary>
    Task<OrderDto?> CancelOrderAsync(string orderId);

    /// <summary>Marks an order as refunded after the payment provider refund has been issued.</summary>
    Task<OrderDto?> RefundOrderAsync(string orderId);
}
