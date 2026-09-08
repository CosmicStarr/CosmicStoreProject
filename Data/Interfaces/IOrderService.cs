using Models.AngularDTOs;

namespace Data.Interfaces;

public interface IOrderService
{
    Task<OrderDto> CreateOrderAsync(AngularCheckoutRequest request, string? userId, string? userEmail);
    Task<IEnumerable<OrderDto>> GetUserOrdersAsync(string userId);
    Task<IEnumerable<OrderDto>> GetAllOrdersAsync();
    Task<OrderDto?> GetOrderAsync(string orderId, string? userId = null);
}
