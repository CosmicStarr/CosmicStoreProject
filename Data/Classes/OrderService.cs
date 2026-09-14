using System.Net;
using Data.Interfaces;
using Data.Util;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Models;
using Models.AngularDTOs;

namespace Data.Classes;

/// <summary>
/// Checkout and order lifecycle: persist paid orders, map line SKUs to CJ vids, cancel/refund locally.
/// CJ create/pay/status calls are implemented but currently skipped.
/// </summary>
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
    private readonly IEmailSender _emailSender;
    private readonly IWebHostEnvironment _env;
    private readonly IConfiguration _configuration;
    private readonly ILogger<OrderService> _logger;

    public OrderService(
        IStoreUnitOfWork storeUnitOfWork,
        ICJDropshippingService cjService,
        IPaymentService paymentService,
        IEmailSender emailSender,
        IWebHostEnvironment env,
        IConfiguration configuration,
        ILogger<OrderService> logger)
    {
        _storeUnitOfWork = storeUnitOfWork;
        _cjService = cjService;
        _paymentService = paymentService;
        _emailSender = emailSender;
        _env = env;
        _configuration = configuration;
        _logger = logger;
    }

    /// <summary>
    /// Creates an order after Stripe confirms payment. Each line SKU is mapped to a vid. CJ fulfillment is not submitted.
    /// </summary>
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
        var contactEmail = string.IsNullOrWhiteSpace(request.Email)
            ? userEmail?.Trim()
            : request.Email.Trim();

        if (string.IsNullOrWhiteSpace(contactEmail))
            throw new InvalidOperationException("An email address is required to confirm this order.");

        var itemNames = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        var order = new Order
        {
            OrderId = Guid.NewGuid().ToString(),
            AppUserId = userId,
            CustomerName = request.FullName,
            CustomerEmail = contactEmail,
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
            var product = await FindProductByLineSkuAsync(item.Sku);

            if (product is null)
                throw new InvalidOperationException($"Product with SKU '{item.Sku}' was not found.");

            itemNames[item.Sku] = string.IsNullOrWhiteSpace(item.Name) ? product.NameEn : item.Name;

            order.Items.Add(new OrderItem
            {
                Sku = item.Sku,
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

        // CJ fulfillment is disabled so test Stripe checkouts do not create real shipments.
        // try
        // {
        //     var cjPayload = new CjCreateOrderV3Request
        //     {
        //         orderNumber = order.OrderId,
        //         shippingCustomerName = request.FullName,
        //         shippingAddress = request.StreetAddress,
        //         shippingCity = request.City,
        //         shippingProvince = request.ProvinceOrState,
        //         shippingCountryCode = request.CountryCode,
        //         shippingCountry = MapCountryName(request.CountryCode),
        //         logisticName = order.LogisticName,
        //         fromCountryCode = _configuration["CJDropshipping:FromCountryCode"] ?? "CN",
        //         products = order.Items.Select(i => new CjOrderProduct
        //         {
        //             vid = i.CjVariantId,
        //             quantity = i.Quantity
        //         }).ToList()
        //     };
        //
        //     var shipmentOrderId = await _cjService.CreateOrderV3Async(cjPayload);
        //     order.CjShipmentOrderId = shipmentOrderId;
        //
        //     var isPaid = await _cjService.PayBalanceV2Async(shipmentOrderId);
        //     order.Status = isPaid ? Status.Processing.ToString() : Status.PaymentOnHold.ToString();
        //     order.LastStatusSyncAt = DateTime.UtcNow;
        // }
        // catch (Exception ex)
        // {
        //     _logger.LogError(ex, "CJ submission failed for order {OrderId}; leaving it queued for retry.", order.OrderId);
        //     order.Status = Status.Submitted.ToString();
        // }
        _logger.LogInformation("Skipping CJ fulfillment for order {OrderId}.", order.OrderId);

        _storeUnitOfWork.Repository<Order>().Update(order);
        await ReserveStockAsync(order);
        await _storeUnitOfWork.Complete();

        var dto = MapToDto(order);
        foreach (var line in dto.Items)
        {
            if (itemNames.TryGetValue(line.Sku, out var name))
            {
                line.Name = name;
            }
        }

        await TrySendOrderConfirmationAsync(dto);
        return dto;
    }

    /// <summary>Lists one customer's orders, newest first.</summary>
    public async Task<IEnumerable<OrderDto>> GetUserOrdersAsync(string userId)
    {
        var orders = await _storeUnitOfWork.Repository<Order>()
            .GetAllParams(
                filter: o => o.AppUserId == userId,
                orderby: q => q.OrderByDescending(o => o.CreatedAt),
                includeProperties: "Items");

        return orders.Select(MapToDto);
    }

    /// <summary>Lists every order for the admin grid, newest first.</summary>
    public async Task<IEnumerable<OrderDto>> GetAllOrdersAsync()
    {
        var orders = await _storeUnitOfWork.Repository<Order>()
            .GetAllParams(
                orderby: q => q.OrderByDescending(o => o.CreatedAt),
                includeProperties: "Items");

        return orders.Select(MapToDto);
    }

    /// <summary>Loads one order. When userId is set, the order must belong to that user.</summary>
    public async Task<OrderDto?> GetOrderAsync(string orderId, string? userId = null)
    {
        var order = await LoadOrderAsync(orderId, userId);
        return order is null ? null : MapToDto(order);
    }

    /// <summary>Would refresh one order from CJ; currently a no-op while fulfillment is off.</summary>
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

    /// <summary>Would poll CJ for open shipments; currently returns 0 while fulfillment is off.</summary>
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

    /// <summary>Marks an unshipped order cancelled and puts reserved stock back. Does not call CJ.</summary>
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
            // CJ fulfillment is disabled; do not cancel a supplier shipment.
            // var cancelled = await _cjService.CancelOrderAsync(order.CjShipmentOrderId);
            // if (!cancelled)
            // {
            //     _logger.LogWarning("CJ refused to cancel shipment {ShipmentOrderId}.", order.CjShipmentOrderId);
            // }
        }

        order.Status = Status.Cancelled.ToString();
        order.LastStatusSyncAt = DateTime.UtcNow;

        _storeUnitOfWork.Repository<Order>().Update(order);
        await ReleaseStockAsync(order);
        await _storeUnitOfWork.Complete();

        return MapToDto(order);
    }

    /// <summary>Sets local status to Refunded. Does not call Stripe or CJ.</summary>
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

    /// <summary>Ensures Stripe reports succeeded and the charged cents match the order total.</summary>
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

    /// <summary>Loads an order with its line items, optionally scoped to one customer.</summary>
    private async Task<Order?> LoadOrderAsync(string orderId, string? userId = null)
    {
        return userId is null
            ? await _storeUnitOfWork.Repository<Order>()
                .GetFirstOrDefault(o => o.OrderId == orderId, includeProperties: "Items")
            : await _storeUnitOfWork.Repository<Order>()
                .GetFirstOrDefault(o => o.OrderId == orderId && o.AppUserId == userId, includeProperties: "Items");
    }

    /// <summary>Would copy CJ status/tracking onto the order. Always false while fulfillment is off.</summary>
    private Task<bool> ApplyRemoteStatusAsync(Order order)
    {
        if (string.IsNullOrWhiteSpace(order.CjShipmentOrderId)) return Task.FromResult(false);

        // CJ fulfillment is disabled; do not poll supplier status.
        return Task.FromResult(false);
        // var remote = await _cjService.GetOrderStatusAsync(order.CjShipmentOrderId);
        // if (remote is null) return false;
        //
        // var mappedStatus = MapCjStatus(remote.OrderStatus) ?? order.Status;
        // var changed = mappedStatus != order.Status || remote.TrackNumber != order.TrackingNumber;
        //
        // order.Status = mappedStatus;
        // if (!string.IsNullOrWhiteSpace(remote.TrackNumber))
        // {
        //     order.TrackingNumber = remote.TrackNumber;
        // }
        // order.LastStatusSyncAt = DateTime.UtcNow;
        //
        // return changed;
    }

    /// <summary>Finds the storefront product for a cart/order SKU (parent SKU, skuPhoto, or variant SKU).</summary>
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

        var remoteVid = await _cjService.ResolveVidBySkuAsync(sku);
        if (!string.IsNullOrWhiteSpace(remoteVid))
        {
            RememberVariantMapping(product, sku, remoteVid, variant);
            return remoteVid;
        }

        return product.CjVariantId ?? product.Id;
    }

    /// <summary>Caches a CJ vid against a store SKU so later orders do not need another CJ lookup.</summary>
    private void RememberVariantMapping(Products product, string sku, string vid, ProductVariant? existing)
    {
        if (existing is not null)
        {
            existing.CjVariantId = vid;
            existing.LastSyncedAt = DateTime.UtcNow;
            _storeUnitOfWork.Repository<ProductVariant>().Update(existing);
            return;
        }

        _storeUnitOfWork.Repository<ProductVariant>().Add(new ProductVariant
        {
            ProductId = product.Id,
            CjVariantId = vid,
            Sku = sku,
            LastSyncedAt = DateTime.UtcNow
        });
    }

    /// <summary>Decrements Products.StockQuantity for each order line after a successful checkout.</summary>
    private async Task ReserveStockAsync(Order order)
    {
        foreach (var item in order.Items)
        {
            var product = await FindProductByLineSkuAsync(item.Sku);

            if (product is null) continue;

            product.StockQuantity = Math.Max(0, product.StockQuantity - item.Quantity);
            _storeUnitOfWork.Repository<Products>().Update(product);
        }
    }

    /// <summary>Adds reserved quantities back when an order is cancelled.</summary>
    private async Task ReleaseStockAsync(Order order)
    {
        foreach (var item in order.Items)
        {
            var product = await FindProductByLineSkuAsync(item.Sku);

            if (product is null) continue;

            product.StockQuantity += item.Quantity;
            _storeUnitOfWork.Repository<Products>().Update(product);
        }
    }

    /// <summary>Maps a CJ shipment status string onto the local Status enum name.</summary>
    private static string? MapCjStatus(string? cjStatus) => cjStatus?.Trim().ToUpperInvariant() switch
    {
        "CREATED" or "UNPAID" => nameof(Status.PaymentOnHold),
        "IN_CANCEL" or "CANCELLED" or "CANCEL" => nameof(Status.Cancelled),
        "UNSHIPPED" or "PROCESSING" or "PENDING" => nameof(Status.Processing),
        "SHIPPED" or "PARTIAL_SHIPPED" or "DELIVERING" => nameof(Status.Shipped),
        "DELIVERED" or "COMPLETED" or "FINISHED" => nameof(Status.Delivered),
        _ => null
    };

    private async Task TrySendOrderConfirmationAsync(OrderDto order)
    {
        if (string.IsNullOrWhiteSpace(order.CustomerEmail))
        {
            return;
        }

        try
        {
            var filePath = Path.Combine(_env.WebRootPath ?? string.Empty, "templates", "OrderConfirmation.html");
            var html = File.Exists(filePath)
                ? await File.ReadAllTextAsync(filePath)
                : "<div><p>Thanks for your order {{ORDER_ID}}.</p>{{ITEMS}}<p>Total {{TOTAL}}</p></div>";

            var itemRows = string.Concat(order.Items.Select(item =>
            {
                var name = WebUtility.HtmlEncode(string.IsNullOrWhiteSpace(item.Name) ? item.Sku : item.Name);
                var lineTotal = (item.PriceAtPurchase * item.Quantity).ToString("0.00", System.Globalization.CultureInfo.InvariantCulture);
                return $"<tr><td style=\"padding:8px 0;border-bottom:1px solid #e2e8f0;\">{name}<br/><span style=\"color:#64748b;font-size:13px;\">{WebUtility.HtmlEncode(item.Sku)} × {item.Quantity}</span></td><td style=\"padding:8px 0;border-bottom:1px solid #e2e8f0;text-align:right;\">${lineTotal}</td></tr>";
            }));

            html = html
                .Replace("{{NAME}}", WebUtility.HtmlEncode(order.CustomerName))
                .Replace("{{EMAIL}}", WebUtility.HtmlEncode(order.CustomerEmail))
                .Replace("{{ORDER_ID}}", WebUtility.HtmlEncode(order.OrderId))
                .Replace("{{ITEMS}}", itemRows)
                .Replace("{{ADDRESS}}", WebUtility.HtmlEncode($"{order.ShippingAddress}, {order.City}, {order.State} {order.Country}"))
                .Replace("{{SHIPPING}}", $"{WebUtility.HtmlEncode(order.LogisticName)} — ${order.ShippingCost.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture)}")
                .Replace("{{TOTAL}}", $"${order.Total.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture)}");

            await _emailSender.SendEmailAsync(order.CustomerEmail, $"Your CosmicStore order {order.OrderId[..Math.Min(8, order.OrderId.Length)]}", html);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send order confirmation for {OrderId}.", order.OrderId);
        }
    }

    /// <summary>Projects an Order entity into the API payload, including computed total.</summary>
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

    /// <summary>Expands an ISO country code to the name CJ's create-order API expects.</summary>
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
