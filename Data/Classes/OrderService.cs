using Data.Interfaces;
using Data.Util;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Models;
using Models.AngularDTOs;

namespace Data.Classes;

public class OrderService : IOrderService
{
    private static readonly string[] TerminalStatuses =
    [
        nameof(Status.Delivered),
        nameof(Status.Cancelled),
        nameof(Status.Refunded)
    ];

    private readonly IStoreUnitOfWork _storeUnitOfWork;
    private readonly ICJDropshippingService _cjService;
    private readonly IPaymentService _paymentService;
    private readonly IConfiguration _configuration;
    private readonly ILogger<OrderService> _logger;

    public OrderService(
        IStoreUnitOfWork storeUnitOfWork,
        ICJDropshippingService cjService,
        IPaymentService paymentService,
        IConfiguration configuration,
        ILogger<OrderService> logger)
    {
        _storeUnitOfWork = storeUnitOfWork;
        _cjService = cjService;
        _paymentService = paymentService;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<OrderDto> CreateOrderAsync(AngularCheckoutRequest request, string? userId, string? userEmail)
    {
        if (request.Items.Count == 0)
            throw new InvalidOperationException("Cart is empty.");

        if (string.IsNullOrWhiteSpace(request.StripePaymentMethodId))
            throw new InvalidOperationException("Payment method is required.");

        if (await _storeUnitOfWork.Repository<Order>()
                .GetFirstOrDefault(o => o.PaymentTransactionId == request.StripePaymentMethodId) is not null)
        {
            throw new InvalidOperationException("An order has already been placed for this payment.");
        }

        var defaultLogistic = _configuration["CJDropshipping:DefaultLogisticName"] ?? "CJPacket Ordinary";

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
            LogisticName = string.IsNullOrWhiteSpace(request.LogisticName) ? defaultLogistic : request.LogisticName,
            ShippingCost = request.ShippingCost,
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
                CjVariantId = await ResolveVariantIdAsync(product, item.Sku),
                Quantity = item.Amount,
                PriceAtPurchase = product.SellPrice
            });
        }

        // Confirm with Stripe that the money actually arrived, and that it covers this exact
        // basket, before committing the order or spending anything from the CJ wallet.
        var expectedTotal = order.Items.Sum(i => i.PriceAtPurchase * i.Quantity) + order.ShippingCost;
        await VerifyPaymentAsync(request.StripePaymentMethodId, expectedTotal);
        order.PaymentStatus = nameof(Status.PaymentRecevied);

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
                logisticName = order.LogisticName,
                fromCountryCode = _configuration["CJDropshipping:FromCountryCode"] ?? "CN",
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
            order.LastStatusSyncAt = DateTime.UtcNow;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "CJ submission failed for order {OrderId}; leaving it queued for retry.", order.OrderId);
            order.Status = Status.Submitted.ToString();
        }

        _storeUnitOfWork.Repository<Order>().Update(order);
        await ReserveStockAsync(order);
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
        var order = await LoadOrderAsync(orderId, userId);
        return order is null ? null : MapToDto(order);
    }

    public async Task<OrderDto?> SyncOrderStatusAsync(string orderId)
    {
        var order = await LoadOrderAsync(orderId);
        if (order is null) return null;

        var updated = await ApplyRemoteStatusAsync(order);
        if (updated)
        {
            _storeUnitOfWork.Repository<Order>().Update(order);
            await _storeUnitOfWork.Complete();
        }

        return MapToDto(order);
    }

    public async Task<int> SyncPendingOrdersAsync(CancellationToken cancellationToken = default)
    {
        var orders = await _storeUnitOfWork.Repository<Order>()
            .GetAllParams(
                new PageParams { PageNumber = 1, PageSize = 50 },
                o => o.CjShipmentOrderId != null && !TerminalStatuses.Contains(o.Status),
                q => q.OrderBy(o => o.LastStatusSyncAt),
                "Items");

        var updatedCount = 0;

        foreach (var order in orders)
        {
            if (cancellationToken.IsCancellationRequested) break;

            if (await ApplyRemoteStatusAsync(order))
            {
                _storeUnitOfWork.Repository<Order>().Update(order);
                updatedCount++;
            }
        }

        if (updatedCount > 0)
        {
            await _storeUnitOfWork.Complete();
        }

        return updatedCount;
    }

    public async Task<OrderDto?> CancelOrderAsync(string orderId)
    {
        var order = await LoadOrderAsync(orderId);
        if (order is null) return null;

        if (order.Status is nameof(Status.Shipped) or nameof(Status.Delivered))
        {
            throw new InvalidOperationException("Shipped orders cannot be cancelled. Issue a refund instead.");
        }

        if (!string.IsNullOrWhiteSpace(order.CjShipmentOrderId))
        {
            var cancelled = await _cjService.CancelOrderAsync(order.CjShipmentOrderId);
            if (!cancelled)
            {
                _logger.LogWarning("CJ refused to cancel shipment {ShipmentOrderId}.", order.CjShipmentOrderId);
            }
        }

        order.Status = Status.Cancelled.ToString();
        order.LastStatusSyncAt = DateTime.UtcNow;

        _storeUnitOfWork.Repository<Order>().Update(order);
        await ReleaseStockAsync(order);
        await _storeUnitOfWork.Complete();

        return MapToDto(order);
    }

    public async Task<OrderDto?> RefundOrderAsync(string orderId)
    {
        var order = await LoadOrderAsync(orderId);
        if (order is null) return null;

        order.Status = Status.Refunded.ToString();
        order.LastStatusSyncAt = DateTime.UtcNow;

        _storeUnitOfWork.Repository<Order>().Update(order);
        await _storeUnitOfWork.Complete();

        return MapToDto(order);
    }

    private async Task VerifyPaymentAsync(string paymentIntentId, decimal expectedTotal)
    {
        var intent = await _paymentService.GetPaymentIntentAsync(paymentIntentId);

        if (intent is null)
            throw new InvalidOperationException("That payment could not be found. Please try again.");

        if (intent.Status != "succeeded")
            throw new InvalidOperationException($"Payment has not completed (status: {intent.Status}).");

        var expectedCents = PaymentService.ToMinorUnits(expectedTotal);
        if (intent.Amount != expectedCents)
        {
            _logger.LogWarning(
                "PaymentIntent {PaymentIntentId} paid {Paid} but the order totals {Expected}.",
                paymentIntentId, intent.Amount, expectedCents);

            throw new InvalidOperationException("The payment amount does not match your order total.");
        }
    }

    private async Task<Order?> LoadOrderAsync(string orderId, string? userId = null)
    {
        return userId is null
            ? await _storeUnitOfWork.Repository<Order>()
                .GetFirstOrDefault(o => o.OrderId == orderId, includeProperties: "Items")
            : await _storeUnitOfWork.Repository<Order>()
                .GetFirstOrDefault(o => o.OrderId == orderId && o.AppUserId == userId, includeProperties: "Items");
    }

    private async Task<bool> ApplyRemoteStatusAsync(Order order)
    {
        if (string.IsNullOrWhiteSpace(order.CjShipmentOrderId)) return false;

        var remote = await _cjService.GetOrderStatusAsync(order.CjShipmentOrderId);
        if (remote is null) return false;

        var mappedStatus = MapCjStatus(remote.OrderStatus) ?? order.Status;
        var changed = mappedStatus != order.Status || remote.TrackNumber != order.TrackingNumber;

        order.Status = mappedStatus;
        if (!string.IsNullOrWhiteSpace(remote.TrackNumber))
        {
            order.TrackingNumber = remote.TrackNumber;
        }
        order.LastStatusSyncAt = DateTime.UtcNow;

        return changed;
    }

    /// <summary>
    /// Resolves the CJ variant id for a SKU, preferring the mapped ProductVariant row and
    /// falling back to the product-level variant id captured at publish time.
    /// </summary>
    private async Task<string> ResolveVariantIdAsync(Products product, string sku)
    {
        var variant = await _storeUnitOfWork.Repository<ProductVariant>()
            .GetFirstOrDefault(v => v.Sku == sku || (v.ProductId == product.Id && v.Sku == product.Sku));

        if (variant is not null && !string.IsNullOrWhiteSpace(variant.CjVariantId))
        {
            return variant.CjVariantId;
        }

        return product.CjVariantId ?? product.Id;
    }

    private async Task ReserveStockAsync(Order order)
    {
        foreach (var item in order.Items)
        {
            var product = await _storeUnitOfWork.Repository<Products>()
                .GetFirstOrDefault(p => p.Sku == item.Sku);

            if (product is null) continue;

            product.StockQuantity = Math.Max(0, product.StockQuantity - item.Quantity);
            _storeUnitOfWork.Repository<Products>().Update(product);
        }
    }

    private async Task ReleaseStockAsync(Order order)
    {
        foreach (var item in order.Items)
        {
            var product = await _storeUnitOfWork.Repository<Products>()
                .GetFirstOrDefault(p => p.Sku == item.Sku);

            if (product is null) continue;

            product.StockQuantity += item.Quantity;
            _storeUnitOfWork.Repository<Products>().Update(product);
        }
    }

    private static string? MapCjStatus(string? cjStatus) => cjStatus?.Trim().ToUpperInvariant() switch
    {
        "CREATED" or "UNPAID" => nameof(Status.PaymentOnHold),
        "IN_CANCEL" or "CANCELLED" or "CANCEL" => nameof(Status.Cancelled),
        "UNSHIPPED" or "PROCESSING" or "PENDING" => nameof(Status.Processing),
        "SHIPPED" or "PARTIAL_SHIPPED" or "DELIVERING" => nameof(Status.Shipped),
        "DELIVERED" or "COMPLETED" or "FINISHED" => nameof(Status.Delivered),
        _ => null
    };

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
            PaymentStatus = order.PaymentStatus,
            LogisticName = order.LogisticName,
            ShippingCost = order.ShippingCost,
            TrackingNumber = order.TrackingNumber,
            LastStatusSyncAt = order.LastStatusSyncAt,
            CreatedAt = order.CreatedAt,
            Total = order.Items.Sum(i => i.PriceAtPurchase * i.Quantity) + order.ShippingCost,
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
        "AU" => "Australia",
        "DE" => "Germany",
        "FR" => "France",
        _ => countryCode
    };
}
