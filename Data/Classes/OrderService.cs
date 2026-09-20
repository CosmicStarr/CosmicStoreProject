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
/// Checkout and order lifecycle: persist Stripe-paid orders, then submit to CJ only when the wallet can cover fulfillment.
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
    /// Creates an order after Stripe confirms payment. CJ fulfillment is submitted only when the wallet can cover it.
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
            ZipCode = request.ZipCode,
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
                Name = itemNames[item.Sku],
                Status = "Ordered",
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

        await ReserveStockAsync(order);
        await TryFulfillWithCjAsync(order);

        _storeUnitOfWork.Repository<Order>().Update(order);
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
        var collected = new List<Order>();
        var page = 1;

        while (true)
        {
            var batch = await _storeUnitOfWork.Repository<Order>()
                .GetAllParams(
                    new PageParams { PageNumber = page, PageSize = 50 },
                    orderby: q => q.OrderByDescending(o => o.CreatedAt),
                    includeProperties: "Items");

            collected.AddRange(batch);
            if (!batch.HasNextPage) break;
            page++;
        }

        return collected.Select(MapToDto).ToList();
    }

    /// <summary>Loads one order. When userId is set, the order must belong to that user.</summary>
    public async Task<OrderDto?> GetOrderAsync(string orderId, string? userId = null)
    {
        var order = await LoadOrderAsync(orderId, userId);
        return order is null ? null : MapToDto(order);
    }

    /// <summary>Retries CJ submit when the wallet can cover it, then refreshes shipment status.</summary>
    public async Task<OrderDto?> SyncOrderStatusAsync(string orderId)
    {
        var order = await LoadOrderAsync(orderId);
        if (order is null) return null;

        var hadShipment = !string.IsNullOrWhiteSpace(order.CjShipmentOrderId);
        var updated = await TryFulfillWithCjAsync(order);
        if (hadShipment && await ApplyRemoteStatusAsync(order))
        {
            updated = true;
        }

        if (updated)
        {
            _storeUnitOfWork.Repository<Order>().Update(order);
            await _storeUnitOfWork.Complete();
        }

        return MapToDto(order);
    }

    /// <summary>
    /// Submits queued Stripe-paid orders when the CJ wallet can cover them, then polls open shipments.
    /// </summary>
    public async Task<int> SyncPendingOrdersAsync(CancellationToken cancellationToken = default)
    {
        var orders = await _storeUnitOfWork.Repository<Order>()
            .GetAllParams(
                new PageParams { PageNumber = 1, PageSize = 50 },
                o => !TerminalStatuses.Contains(o.Status),
                q => q.OrderBy(o => o.LastStatusSyncAt),
                "Items");

        var updatedCount = 0;

        foreach (var order in orders)
        {
            if (cancellationToken.IsCancellationRequested) break;

            var hadShipment = !string.IsNullOrWhiteSpace(order.CjShipmentOrderId);
            var updated = await TryFulfillWithCjAsync(order);
            if (hadShipment && await ApplyRemoteStatusAsync(order))
            {
                updated = true;
            }

            if (updated)
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

    /// <summary>Cancels an unshipped order with CJ when a shipment exists, then restocks.</summary>
    public async Task<OrderDto?> CancelOrderAsync(string orderId)
    {
        var order = await LoadOrderAsync(orderId);
        if (order is null) return null;

        if (order.Status is nameof(Status.Cancelled) or nameof(Status.Refunded))
        {
            return MapToDto(order);
        }

        await EnsureUnfulfilledAtCjAsync(order);

        var cjCancelled = await TryCancelCjShipmentAsync(order);
        if (!cjCancelled)
        {
            throw new InvalidOperationException(
                "The supplier could not cancel this shipment. Please try again or contact support.");
        }

        await ReleaseStockAsync(order);
        MarkItems(order, Status.Cancelled.ToString());

        order.Status = Status.Cancelled.ToString();
        order.LastStatusSyncAt = DateTime.UtcNow;

        _storeUnitOfWork.Repository<Order>().Update(order);
        await _storeUnitOfWork.Complete();

        return MapToDto(order);
    }

    /// <summary>
    /// Customer cancellation after a live CJ status check. Unfulfilled orders are cancelled on CJ
    /// and refunded through Stripe. Shipped orders are refused.
    /// </summary>
    public async Task<OrderDto?> RequestCancellationAsync(string orderId, string userId)
    {
        var order = await LoadOrderAsync(orderId, userId);
        if (order is null) return null;

        if (order.Status is nameof(Status.Cancelled) or nameof(Status.Refunded))
        {
            return MapToDto(order);
        }

        await EnsureUnfulfilledAtCjAsync(order);

        var cjCancelled = await TryCancelCjShipmentAsync(order);
        if (!cjCancelled)
        {
            throw new InvalidOperationException(
                "The supplier could not cancel this shipment. Please try again or contact support.");
        }

        if (!string.IsNullOrWhiteSpace(order.PaymentTransactionId)
            && order.PaymentStatus is not (nameof(Status.PaymentFailed) or nameof(Status.Pending)))
        {
            await _paymentService.RefundPaymentAsync(order.PaymentTransactionId);
            order.PaymentStatus = nameof(Status.Refunded);
        }

        await ReleaseStockAsync(order);
        MarkItems(order, Status.Cancelled.ToString());

        order.Status = Status.Cancelled.ToString();
        order.LastStatusSyncAt = DateTime.UtcNow;

        _storeUnitOfWork.Repository<Order>().Update(order);
        await _storeUnitOfWork.Complete();

        return MapToDto(order);
    }

    /// <summary>
    /// Cancels one line and refunds that line (plus shipping when it is the last active item).
    /// Remaining items stay on the order. An unshipped CJ shipment is cancelled so it can be resubmitted without the line.
    /// </summary>
    public async Task<OrderDto?> CancelOrderItemAsync(string orderId, int itemId)
    {
        var order = await LoadOrderAsync(orderId);
        if (order is null) return null;

        if (order.Status is nameof(Status.Cancelled) or nameof(Status.Refunded))
        {
            return MapToDto(order);
        }

        await EnsureUnfulfilledAtCjAsync(order);

        var item = order.Items.FirstOrDefault(line => line.Id == itemId);
        if (item is null)
        {
            throw new InvalidOperationException("That item is not on this order.");
        }

        if (!IsActiveItem(item))
        {
            return MapToDto(order);
        }

        if (string.IsNullOrWhiteSpace(order.PaymentTransactionId))
        {
            throw new InvalidOperationException("This order has no Stripe payment to refund for that item.");
        }

        if (!string.IsNullOrWhiteSpace(order.CjShipmentOrderId))
        {
            var cjCancelled = await TryCancelCjShipmentAsync(order);
            if (!cjCancelled)
            {
                throw new InvalidOperationException(
                    "This order is already at CJ and that shipment could not be updated. Cancel the whole order instead.");
            }

            order.CjShipmentOrderId = null;
            order.TrackingNumber = null;
            if (order.Status is nameof(Status.Processing) or nameof(Status.Submitted))
            {
                order.Status = Status.PaymentRecevied.ToString();
            }
        }

        var remainingActive = order.Items.Count(IsActiveItem);
        var refundAmount = item.PriceAtPurchase * item.Quantity;
        if (remainingActive == 1)
        {
            refundAmount += order.ShippingCost;
        }

        await _paymentService.RefundPaymentAsync(
            order.PaymentTransactionId,
            PaymentService.ToMinorUnits(refundAmount),
            $"order-item-refund-{order.OrderId}-{item.Id}");

        await ReleaseStockForItemAsync(item);
        item.Status = Status.Cancelled.ToString();
        _storeUnitOfWork.Repository<OrderItem>().Update(item);

        if (!order.Items.Any(IsActiveItem))
        {
            order.Status = Status.Cancelled.ToString();
            order.PaymentStatus = nameof(Status.Refunded);
        }

        order.LastStatusSyncAt = DateTime.UtcNow;
        _storeUnitOfWork.Repository<Order>().Update(order);
        await _storeUnitOfWork.Complete();

        return MapToDto(order);
    }

    /// <summary>Refunds the Stripe PaymentIntent, then marks the order refunded and restocks when it never shipped.</summary>
    public async Task<OrderDto?> RefundOrderAsync(string orderId)
    {
        var order = await LoadOrderAsync(orderId);
        if (order is null) return null;

        if (IsLocallyRefunded(order))
        {
            return MapToDto(order);
        }

        if (string.IsNullOrWhiteSpace(order.PaymentTransactionId))
        {
            throw new InvalidOperationException("This order has no Stripe payment to refund.");
        }

        if (order.PaymentStatus is nameof(Status.PaymentFailed) or nameof(Status.Pending))
        {
            throw new InvalidOperationException("This order was never charged, so there is nothing to refund.");
        }

        await TryCancelCjShipmentAsync(order);
        await _paymentService.RefundPaymentAsync(order.PaymentTransactionId);
        await ApplyLocalRefundAsync(order);
        return MapToDto(order);
    }

    /// <summary>Marks the order refunded after Stripe already returned the money (Dashboard or webhook).</summary>
    public async Task<OrderDto?> ApplyPaymentRefundedAsync(string paymentIntentId)
    {
        var order = await _storeUnitOfWork.Repository<Order>()
            .GetFirstOrDefault(o => o.PaymentTransactionId == paymentIntentId, includeProperties: "Items");

        if (order is null) return null;
        if (IsLocallyRefunded(order)) return MapToDto(order);

        await TryCancelCjShipmentAsync(order);
        await ApplyLocalRefundAsync(order);
        return MapToDto(order);
    }

    /// <summary>True when both fulfillment and payment already show Refunded.</summary>
    private static bool IsLocallyRefunded(Order order) =>
        order.Status == nameof(Status.Refunded) && order.PaymentStatus == nameof(Status.Refunded);

    /// <summary>
    /// Sets Refunded on the order and payment. Restocks only when reserved inventory was not
    /// already released (cancel) and the goods have not left the warehouse.
    /// </summary>
    private async Task ApplyLocalRefundAsync(Order order)
    {
        var shouldRestock = order.Status is not (
            nameof(Status.Refunded) or
            nameof(Status.Cancelled) or
            nameof(Status.Shipped) or
            nameof(Status.Delivered));

        if (shouldRestock)
        {
            await ReleaseStockAsync(order);
        }

        MarkItems(order, Status.Refunded.ToString());
        order.Status = Status.Refunded.ToString();
        order.PaymentStatus = nameof(Status.Refunded);
        order.LastStatusSyncAt = DateTime.UtcNow;

        _storeUnitOfWork.Repository<Order>().Update(order);
        await _storeUnitOfWork.Complete();
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

        var postalCheck = intent.LatestCharge?.PaymentMethodDetails?.Card?.Checks?.AddressPostalCodeCheck;
        if (string.Equals(postalCheck, "fail", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "The ZIP / postal code did not match this card. Please check it and try again.");
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

    /// <summary>
    /// Sends or pays the CJ shipment only when the wallet can cover estimated product + freight cost.
    /// Stripe payment stays PaymentRecevied; the order is not placed on hold.
    /// </summary>
    private async Task<bool> TryFulfillWithCjAsync(Order order)
    {
        if (IsTerminalOrShipped(order.Status) || !order.Items.Any(IsActiveItem))
        {
            return false;
        }

        if (order.Status is nameof(Status.Processing)
            && !string.IsNullOrWhiteSpace(order.CjShipmentOrderId))
        {
            return false;
        }

        var estimate = await EstimateCjFulfillmentCostAsync(order);
        if (!await CanCoverCjFulfillmentAsync(estimate))
        {
            _logger.LogInformation(
                "Skipping CJ submit for order {OrderId}; wallet cannot cover estimated fulfillment {Estimate}. Stripe payment is unchanged.",
                order.OrderId,
                estimate);

            if (order.Status == nameof(Status.PaymentOnHold))
            {
                order.Status = Status.PaymentRecevied.ToString();
                order.LastStatusSyncAt = DateTime.UtcNow;
                return true;
            }

            return false;
        }

        try
        {
            if (string.IsNullOrWhiteSpace(order.CjShipmentOrderId))
            {
                order.CjShipmentOrderId = await _cjService.CreateOrderV3Async(BuildCjCreateRequest(order));
            }

            var isPaid = await _cjService.PayBalanceV2Async(order.CjShipmentOrderId);
            order.Status = isPaid ? Status.Processing.ToString() : Status.Submitted.ToString();
            order.LastStatusSyncAt = DateTime.UtcNow;

            if (!isPaid)
            {
                _logger.LogWarning(
                    "CJ created shipment {ShipmentOrderId} for order {OrderId} but wallet pay did not complete.",
                    order.CjShipmentOrderId,
                    order.OrderId);
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "CJ submission failed for order {OrderId}; leaving it queued for retry.", order.OrderId);
            order.Status = Status.Submitted.ToString();
            order.LastStatusSyncAt = DateTime.UtcNow;
            return true;
        }
    }

    /// <summary>True when the CJ wallet is readable and can cover this fulfillment estimate.</summary>
    private async Task<bool> CanCoverCjFulfillmentAsync(decimal estimate)
    {
        var wallet = await _cjService.GetWalletBalanceAsync();
        if (wallet is null)
        {
            _logger.LogWarning("CJ wallet balance is unavailable; not sending orders to CJ.");
            return false;
        }

        if (wallet.Amount <= 0m)
        {
            return false;
        }

        return estimate <= 0m || wallet.Amount >= estimate;
    }

    /// <summary>
    /// Estimates what CJ will charge: variant cost (or retail / markup) plus the selected freight.
    /// </summary>
    private async Task<decimal> EstimateCjFulfillmentCostAsync(Order order)
    {
        var markup = _configuration.GetValue("StoreSettings:DefaultMarkup", 2.0m);
        if (markup <= 0m)
        {
            markup = 1m;
        }

        decimal goods = 0m;
        foreach (var item in order.Items.Where(IsActiveItem))
        {
            var variant = await _storeUnitOfWork.Repository<ProductVariant>()
                .GetFirstOrDefault(v =>
                    v.Sku == item.Sku
                    || (!string.IsNullOrWhiteSpace(item.CjVariantId) && v.CjVariantId == item.CjVariantId));

            var unitCost = variant is not null && variant.CjPrice > 0m
                ? variant.CjPrice
                : item.PriceAtPurchase / markup;

            goods += unitCost * item.Quantity;
        }

        return goods + order.ShippingCost;
    }

    /// <summary>Builds the CJ create-order payload from a persisted store order.</summary>
    private CjCreateOrderV3Request BuildCjCreateRequest(Order order)
    {
        return new CjCreateOrderV3Request
        {
            orderNumber = order.OrderId,
            shippingCustomerName = order.CustomerName,
            shippingAddress = order.ShippingAddress,
            shippingCity = order.City,
            shippingProvince = order.State,
            shippingCountryCode = order.Country,
            shippingCountry = MapCountryName(order.Country),
            logisticName = order.LogisticName,
            fromCountryCode = _configuration["CJDropshipping:FromCountryCode"] ?? "CN",
            products = order.Items.Where(IsActiveItem).Select(item => new CjOrderProduct
            {
                vid = item.CjVariantId,
                quantity = item.Quantity
            }).ToList()
        };
    }

    /// <summary>Asks CJ to cancel an unshipped shipment. Failures are logged and do not block local cancel/refund.</summary>
    private async Task<bool> TryCancelCjShipmentAsync(Order order)
    {
        if (string.IsNullOrWhiteSpace(order.CjShipmentOrderId))
        {
            return true;
        }

        if (order.Status is nameof(Status.Shipped) or nameof(Status.Delivered))
        {
            return false;
        }

        var cancelled = await _cjService.CancelOrderAsync(order.CjShipmentOrderId);
        if (!cancelled)
        {
            _logger.LogWarning("CJ refused to cancel shipment {ShipmentOrderId}.", order.CjShipmentOrderId);
        }

        return cancelled;
    }

    /// <summary>Copies CJ status/tracking onto the order when a shipment id exists.</summary>
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

    /// <summary>True for statuses that should never be sent to CJ again.</summary>
    private static bool IsTerminalOrShipped(string status) =>
        status is nameof(Status.Delivered)
            or nameof(Status.Cancelled)
            or nameof(Status.Refunded)
            or nameof(Status.Shipped);

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

    /// <summary>Decrements Products.StockQuantity for each distinct product after a successful checkout.</summary>
    private async Task ReserveStockAsync(Order order)
    {
        foreach (var (product, quantity) in await LoadStockTargetsAsync(order))
        {
            product.StockQuantity = Math.Max(0, product.StockQuantity - quantity);
            _storeUnitOfWork.Repository<Products>().Update(product);
        }
    }

    /// <summary>Adds reserved quantities back when an order is cancelled.</summary>
    private async Task ReleaseStockAsync(Order order)
    {
        foreach (var (product, quantity) in await LoadStockTargetsAsync(order))
        {
            product.StockQuantity += quantity;
            _storeUnitOfWork.Repository<Products>().Update(product);
        }
    }

    /// <summary>
    /// Loads one product instance per storefront id so two cart SKUs of the same product
    /// cannot attach duplicate Products rows to the change tracker.
    /// </summary>
    private async Task<List<(Products Product, int Quantity)>> LoadStockTargetsAsync(Order order)
    {
        var products = new Dictionary<string, Products>(StringComparer.OrdinalIgnoreCase);
        var quantities = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        foreach (var item in order.Items.Where(IsActiveItem))
        {
            var product = await FindProductByLineSkuAsync(item.Sku);
            if (product is null)
            {
                continue;
            }

            products.TryAdd(product.Id, product);
            quantities[product.Id] = quantities.GetValueOrDefault(product.Id) + item.Quantity;
        }

        return products
            .Select(pair => (pair.Value, quantities[pair.Key]))
            .ToList();
    }

    private const string ShippedCancelMessage =
        "This order has already shipped, so it cannot be cancelled. Please wait for it to arrive and then process a standard return.";

    /// <summary>
    /// Asks CJ for the live shipment status. Allows cancel when there is no shipment yet or CJ
    /// still reports pending/unfulfilled. Shipped or delivered shipments are refused.
    /// </summary>
    private async Task EnsureUnfulfilledAtCjAsync(Order order)
    {
        if (string.IsNullOrWhiteSpace(order.CjShipmentOrderId))
        {
            if (order.Status is nameof(Status.Shipped) or nameof(Status.Delivered))
            {
                throw new InvalidOperationException(ShippedCancelMessage);
            }

            return;
        }

        var remote = await _cjService.GetOrderStatusAsync(order.CjShipmentOrderId);
        if (remote is null)
        {
            throw new InvalidOperationException(
                "We could not confirm this order's fulfillment status with the supplier. Please try again in a few minutes.");
        }

        if (!string.IsNullOrWhiteSpace(remote.TrackNumber))
        {
            order.TrackingNumber = remote.TrackNumber;
        }

        var mapped = MapCjStatus(remote.OrderStatus);
        if (!string.IsNullOrWhiteSpace(mapped))
        {
            order.Status = mapped;
        }

        order.LastStatusSyncAt = DateTime.UtcNow;
        _storeUnitOfWork.Repository<Order>().Update(order);
        await _storeUnitOfWork.Complete();

        if (IsCjShippedOrDelivered(remote.OrderStatus)
            || order.Status is nameof(Status.Shipped) or nameof(Status.Delivered))
        {
            throw new InvalidOperationException(ShippedCancelMessage);
        }
    }

    /// <summary>True when CJ reports the parcel has left the warehouse or is already delivered.</summary>
    private static bool IsCjShippedOrDelivered(string? cjStatus) =>
        cjStatus?.Trim().ToUpperInvariant() is
            "SHIPPED" or "PARTIAL_SHIPPED" or "DELIVERING" or "DELIVERED" or "COMPLETED" or "FINISHED";

    /// <summary>Maps a CJ shipment status string onto the local Status enum name.</summary>
    private static string? MapCjStatus(string? cjStatus) => cjStatus?.Trim().ToUpperInvariant() switch
    {
        "CREATED" or "UNPAID" => nameof(Status.Submitted),
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
                .Replace("{{ADDRESS}}", WebUtility.HtmlEncode($"{order.ShippingAddress}, {order.City}, {order.State} {order.ZipCode} {order.Country}"))
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
            ZipCode = order.ZipCode,
            Country = order.Country,
            PaymentStatus = order.PaymentStatus,
            LogisticName = order.LogisticName,
            ShippingCost = order.ShippingCost,
            TrackingNumber = order.TrackingNumber,
            LastStatusSyncAt = order.LastStatusSyncAt,
            CreatedAt = order.CreatedAt,
            Total = RemainingTotal(order),
            Items = order.Items.Select(i => new OrderItemDto
            {
                Id = i.Id,
                Sku = i.Sku,
                Name = i.Name,
                Status = string.IsNullOrWhiteSpace(i.Status) ? "Ordered" : i.Status,
                Quantity = i.Quantity,
                PriceAtPurchase = i.PriceAtPurchase
            }).ToList()
        };
    }

    /// <summary>True when the line is still fulfillable (not cancelled or refunded).</summary>
    private static bool IsActiveItem(OrderItem item) =>
        item.Status is not (nameof(Status.Cancelled) or nameof(Status.Refunded));

    /// <summary>Marks remaining active lines with the given status.</summary>
    private void MarkItems(Order order, string status)
    {
        foreach (var item in order.Items.Where(IsActiveItem))
        {
            item.Status = status;
            _storeUnitOfWork.Repository<OrderItem>().Update(item);
        }
    }

    /// <summary>Puts one line's reserved quantity back on the storefront product.</summary>
    private async Task ReleaseStockForItemAsync(OrderItem item)
    {
        var product = await FindProductByLineSkuAsync(item.Sku);
        if (product is null) return;

        product.StockQuantity += item.Quantity;
        _storeUnitOfWork.Repository<Products>().Update(product);
    }

    /// <summary>Active line totals plus shipping when anything is still on the order.</summary>
    private static decimal RemainingTotal(Order order)
    {
        var active = order.Items.Where(IsActiveItem).ToList();
        var goods = active.Sum(item => item.PriceAtPurchase * item.Quantity);
        return active.Count > 0 ? goods + order.ShippingCost : 0m;
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
