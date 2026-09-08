using Data.Interfaces;
using Microsoft.EntityFrameworkCore;
using Models;
using Models.AngularDTOs;

namespace Data.Classes;

public class OrderService : IOrderService
{
    private readonly IStoreUnitOfWork _storeUnitOfWork;
    private readonly ICJDropshippingService _cjService;

    public OrderService(IStoreUnitOfWork storeUnitOfWork, ICJDropshippingService cjService)
    {
        _storeUnitOfWork = storeUnitOfWork;
        _cjService = cjService;
    }

    public async Task<OrderDto> CreateOrderAsync(AngularCheckoutRequest request, string? userId, string? userEmail)
    {
        if (request.Items.Count == 0)
            throw new InvalidOperationException("Cart is empty.");

        if (string.IsNullOrWhiteSpace(request.StripePaymentMethodId))
            throw new InvalidOperationException("Payment method is required.");

        var order = new Order
        {
            OrderId = Guid.NewGuid().ToString(),
            AppUserId = userId,
            CustomerName = request.FullName,
            CustomerEmail = userEmail ?? string.Empty,
            ShippingAddress = request.StreetAddress,
            City = request.City,
            State = request.ProvinceOrState,
            Country = request.CountryCode,
            Status = Status.PaymentRecevied.ToString(),
            PaymentTransactionId = request.StripePaymentMethodId,
            CreatedAt = DateTime.UtcNow
        };

        foreach (var item in request.Items)
        {
            var product = await _storeUnitOfWork.Repository<Products>()
                .GetFirstOrDefault(p => p.Sku == item.Sku);

            if (product is null)
                throw new InvalidOperationException($"Product with SKU '{item.Sku}' was not found.");

            order.Items.Add(new OrderItem
            {
                Sku = product.Sku,
                CjVariantId = product.CjVariantId ?? product.Id,
                Quantity = item.Amount,
                PriceAtPurchase = product.SellPrice
            });
        }

        _storeUnitOfWork.Repository<Order>().Add(order);
        await _storeUnitOfWork.Complete();

        try
        {
            var cjPayload = new CjCreateOrderV3Request
            {
                orderNumber = order.OrderId,
                shippingCustomerName = request.FullName,
                shippingAddress = request.StreetAddress,
                shippingCity = request.City,
                shippingProvince = request.ProvinceOrState,
                shippingCountryCode = request.CountryCode,
                shippingCountry = MapCountryName(request.CountryCode),
                logisticName = "CJPacket Ordinary",
                fromCountryCode = "CN",
                products = order.Items.Select(i => new CjOrderProduct
                {
                    vid = i.CjVariantId,
                    quantity = i.Quantity
                }).ToList()
            };

            var shipmentOrderId = await _cjService.CreateOrderV3Async(cjPayload);
            order.CjShipmentOrderId = shipmentOrderId;

            var isPaid = await _cjService.PayBalanceV2Async(shipmentOrderId);
            order.Status = isPaid ? Status.Processing.ToString() : Status.PaymentOnHold.ToString();
        }
        catch
        {
            order.Status = Status.Submitted.ToString();
        }

        _storeUnitOfWork.Repository<Order>().Update(order);
        await _storeUnitOfWork.Complete();

        return MapToDto(order);
    }

    public async Task<IEnumerable<OrderDto>> GetUserOrdersAsync(string userId)
    {
        var orders = await _storeUnitOfWork.Repository<Order>()
            .GetAllParams(
                filter: o => o.AppUserId == userId,
                orderby: q => q.OrderByDescending(o => o.CreatedAt),
                includeProperties: "Items");

        return orders.Select(MapToDto);
    }

    public async Task<IEnumerable<OrderDto>> GetAllOrdersAsync()
    {
        var orders = await _storeUnitOfWork.Repository<Order>()
            .GetAllParams(
                orderby: q => q.OrderByDescending(o => o.CreatedAt),
                includeProperties: "Items");

        return orders.Select(MapToDto);
    }

    public async Task<OrderDto?> GetOrderAsync(string orderId, string? userId = null)
    {
        var order = userId is null
            ? await _storeUnitOfWork.Repository<Order>()
                .GetFirstOrDefault(o => o.OrderId == orderId, includeProperties: "Items")
            : await _storeUnitOfWork.Repository<Order>()
                .GetFirstOrDefault(o => o.OrderId == orderId && o.AppUserId == userId, includeProperties: "Items");

        return order is null ? null : MapToDto(order);
    }

    private static OrderDto MapToDto(Order order)
    {
        return new OrderDto
        {
            OrderId = order.OrderId,
            CjShipmentOrderId = order.CjShipmentOrderId,
            Status = order.Status,
            CustomerName = order.CustomerName,
            CustomerEmail = order.CustomerEmail,
            ShippingAddress = order.ShippingAddress,
            City = order.City,
            State = order.State,
            Country = order.Country,
            CreatedAt = order.CreatedAt,
            Total = order.Items.Sum(i => i.PriceAtPurchase * i.Quantity),
            Items = order.Items.Select(i => new OrderItemDto
            {
                Sku = i.Sku,
                Quantity = i.Quantity,
                PriceAtPurchase = i.PriceAtPurchase
            }).ToList()
        };
    }

    private static string MapCountryName(string countryCode) => countryCode.ToUpperInvariant() switch
    {
        "US" => "United States",
        "CA" => "Canada",
        "GB" => "United Kingdom",
        _ => countryCode
    };
}
