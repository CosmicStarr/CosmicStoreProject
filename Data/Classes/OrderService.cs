using System.Linq.Expressions;
using System.Net;
using System.Security.Cryptography;
using System.Text;
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

    /// <summary>
    /// Storefront claim window from purchase. Long enough for a US lost-package wait
    /// (45 days after the warehouse ships) plus ordinary transit.
    /// </summary>
    private const int RefundWindowDays = 75;
    private const string RefundRequestedStatus = "RefundRequested";

    private readonly IStoreUnitOfWork _storeUnitOfWork;
    private readonly ICJDropshippingService _cjService;
    private readonly IPaymentService _paymentService;
    private readonly IWishlistRegistryService _registryService;
    private readonly IEmailSender _emailSender;
    private readonly IWebHostEnvironment _env;
    private readonly IConfiguration _configuration;
    private readonly IStoreSettingsService _storeSettingsService;
    private readonly ILogger<OrderService> _logger;

    public OrderService(
        IStoreUnitOfWork storeUnitOfWork,
        ICJDropshippingService cjService,
        IPaymentService paymentService,
        IWishlistRegistryService registryService,
        IEmailSender emailSender,
        IWebHostEnvironment env,
        IConfiguration configuration,
        IStoreSettingsService storeSettingsService,
        ILogger<OrderService> logger)
    {
        _storeUnitOfWork = storeUnitOfWork;
        _cjService = cjService;
        _paymentService = paymentService;
        _registryService = registryService;
        _emailSender = emailSender;
        _env = env;
        _configuration = configuration;
        _storeSettingsService = storeSettingsService;
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

        var acceptedTermsAt = LegalTerms.RequireAcceptance(request.AcceptedTermsVersion, request.AcceptedTermsAt);

        var wishlistId = request.WishlistId ?? WishlistRegistryService.BoundWishlistId(request.Items);
        UserAddress? registryAddress = null;
        if (wishlistId is int registryId)
        {
            var registry = await _registryService.RequirePublicRegistryAsync(registryId);
            registryAddress = registry.ShippingAddress;
        }
        else if (string.IsNullOrWhiteSpace(request.StreetAddress)
            || string.IsNullOrWhiteSpace(request.City)
            || string.IsNullOrWhiteSpace(request.ZipCode)
            || string.IsNullOrWhiteSpace(request.CountryCode))
        {
            throw new InvalidOperationException("A shipping address is required.");
        }

        var itemNames = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        var order = new Order
        {
            OrderId = Guid.NewGuid().ToString(),
            AppUserId = userId,
            CustomerName = registryAddress?.FullName ?? request.FullName,
            CustomerEmail = contactEmail,
            ShippingAddress = registryAddress?.StreetAddress ?? request.StreetAddress,
            City = registryAddress?.City ?? request.City,
            State = registryAddress?.ProvinceOrState ?? request.ProvinceOrState,
            ZipCode = registryAddress?.ZipCode ?? request.ZipCode,
            Country = registryAddress?.CountryCode ?? request.CountryCode,
            WishlistId = wishlistId,
            AcceptedTermsAt = acceptedTermsAt,
            AcceptedTermsVersion = LegalTerms.CurrentVersion,
            Status = Status.PaymentRecevied.ToString(),
            PaymentTransactionId = request.StripePaymentMethodId,
            LogisticName = string.IsNullOrWhiteSpace(request.LogisticName) ? defaultLogistic : request.LogisticName,
            ShippingCost = request.ShippingCost,
            CreatedAt = DateTime.UtcNow
        };

        string? guestToken = null;
        if (string.IsNullOrWhiteSpace(userId))
        {
            guestToken = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
            order.GuestAccessTokenHash = HashGuestToken(guestToken);
            order.GuestAccessTokenExpiresAt = DateTime.UtcNow.AddDays(RefundWindowDays);
        }

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
                PriceAtPurchase = await ResolveLinePriceAsync(product, item.Sku)
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
        if (!string.IsNullOrWhiteSpace(guestToken))
        {
            dto.GuestManageUrl = BuildGuestManageUrl(order.OrderId, guestToken);
        }

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

    /// <summary>Lists one customer's orders from the last three months, newest first.</summary>
    public async Task<IEnumerable<OrderDto>> GetUserOrdersAsync(string userId)
    {
        await PurgeExpiredOrdersAsync();
        var cutoff = RetentionCutoffUtc();
        var orders = await CollectOrdersAsync(order => order.AppUserId == userId && order.CreatedAt >= cutoff);
        return orders
            .OrderByDescending(order => order.CreatedAt)
            .Select(order => MapToDto(order));
    }

    /// <summary>Lists every remaining order for the admin grid, newest first.</summary>
    public async Task<IEnumerable<OrderDto>> GetAllOrdersAsync()
    {
        await PurgeExpiredOrdersAsync();
        var orders = await CollectOrdersAsync(null);
        return orders
            .OrderByDescending(order => order.CreatedAt)
            .Select(order => MapToDto(order, revealShippingAddress: true))
            .ToList();
    }

    /// <summary>Loads one order. When userId is set, the order must belong to that user and still be within retention.</summary>
    public async Task<OrderDto?> GetOrderAsync(string orderId, string? userId = null)
    {
        var order = await LoadOrderAsync(orderId, userId);
        if (order is null) return null;
        if (userId is not null && order.CreatedAt < RetentionCutoffUtc())
        {
            return null;
        }

        return MapToDto(order, revealShippingAddress: userId is null);
    }

    /// <summary>Removes orders whose created date is older than three months. Line items cascade with the order.</summary>
    public async Task<int> PurgeExpiredOrdersAsync()
    {
        var cutoff = RetentionCutoffUtc();
        var expired = await CollectOrdersAsync(order => order.CreatedAt < cutoff, includeProperties: null);
        if (expired.Count == 0)
        {
            return 0;
        }

        _storeUnitOfWork.Repository<Order>().RemoveRange(expired);
        await _storeUnitOfWork.Complete();
        return expired.Count;
    }

    /// <summary>Picks up to three still-listed products this customer bought in the last three months.</summary>
    public async Task<IReadOnlyList<ProductResponseDto>> GetRecentPurchasesAsync(string userId)
    {
        await PurgeExpiredOrdersAsync();
        var cutoff = RetentionCutoffUtc();
        var orders = await CollectOrdersAsync(order => order.AppUserId == userId && order.CreatedAt >= cutoff);
        var skus = orders
            .SelectMany(order => order.Items)
            .Where(IsActiveItem)
            .Select(item => item.Sku)
            .Where(sku => !string.IsNullOrWhiteSpace(sku))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(_ => Random.Shared.Next())
            .ToList();

        var picks = new List<ProductResponseDto>();
        var seenProducts = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var sku in skus)
        {
            if (picks.Count >= 3)
            {
                break;
            }

            var product = await FindProductByLineSkuAsync(sku);
            if (product is null || !seenProducts.Add(product.Id))
            {
                continue;
            }

            picks.Add(EditCjProducts.ToResponse(product));
        }

        return picks;
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

        CjDisputeDto? dispute = null;
        if (!string.IsNullOrWhiteSpace(order.CjDisputeId) || RequiresReturnReceipt(order))
        {
            dispute = await LoadLiveDisputeAsync(order);
            if (dispute is not null)
            {
                ApplyDispute(order, dispute);
                updated = true;
            }
        }

        if (updated)
        {
            _storeUnitOfWork.Repository<Order>().Update(order);
            await _storeUnitOfWork.Complete();
        }

        return MapToDto(order, dispute, revealShippingAddress: true);
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
    public async Task<OrderDto?> RequestCancellationAsync(string orderId, string? userId = null)
    {
        var order = await LoadOrderAsync(orderId, userId);
        if (order is null) return null;

        if (order.Status is nameof(Status.Cancelled) or nameof(Status.Refunded))
        {
            await InvalidateGuestAccessTokenAsync(order);
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
            var refundedCents = await _paymentService.RefundPaymentAsync(order.PaymentTransactionId);
            order.PaymentStatus = nameof(Status.Refunded);
            await OnGuestStripeRefundSucceededAsync(order, refundedCents);
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
    public async Task<OrderDto?> CancelOrderItemAsync(string orderId, int itemId, string? userId = null)
    {
        var order = await LoadOrderAsync(orderId, userId);
        if (order is null) return null;

        if (order.Status is nameof(Status.Cancelled) or nameof(Status.Refunded))
        {
            await InvalidateGuestAccessTokenAsync(order);
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
                throw new InvalidOperationException(userId is null
                    ? "This order is already at CJ and that shipment could not be updated. Cancel the whole order instead."
                    : "The supplier could not update this shipment. Please try cancelling the whole order, or try again in a few minutes.");
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

        var refundedCents = await _paymentService.RefundPaymentAsync(
            order.PaymentTransactionId,
            PaymentService.ToMinorUnits(refundAmount),
            $"order-item-refund-{order.OrderId}-{item.Id}");
        await OnGuestStripeRefundSucceededAsync(order, refundedCents);

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

    /// <summary>
    /// Records a per-item refund request (reason required; return tracking only if we asked for a domestic return).
    /// Allowed within the storefront claim window, and only after the order has shipped.
    /// </summary>
    public async Task<OrderDto?> RequestItemRefundAsync(
        string orderId,
        int itemId,
        string? userId,
        RequestRefundRequest? request)
    {
        var order = await LoadOrderAsync(orderId, userId);
        if (order is null) return null;

        request ??= new RequestRefundRequest();

        if (order.Status is nameof(Status.Cancelled) or nameof(Status.Refunded)
            || order.PaymentStatus is nameof(Status.Refunded))
        {
            throw new InvalidOperationException("This order is already closed, so a refund cannot be requested.");
        }

        if (order.PaymentStatus is nameof(Status.PaymentFailed) or nameof(Status.Pending))
        {
            throw new InvalidOperationException("This order was never charged, so there is nothing to refund.");
        }

        if (!IsWithinRefundWindow(order))
        {
            throw new InvalidOperationException(
                $"Refund claims can only be filed within {RefundWindowDays} days of purchase. That window closed on {RefundWindowEnd(order):MMMM d, yyyy}.");
        }

        if (!RequiresReturnReceipt(order))
        {
            throw new InvalidOperationException(
                "This order has not shipped yet. Cancel the item instead of requesting a return refund.");
        }

        var item = order.Items.FirstOrDefault(line => line.Id == itemId);
        if (item is null)
        {
            throw new InvalidOperationException("That item is not on this order.");
        }

        if (item.Status is nameof(Status.Cancelled) or nameof(Status.Refunded))
        {
            throw new InvalidOperationException("That item is already cancelled or refunded.");
        }

        var reason = request.Reason?.Trim();
        if (string.IsNullOrWhiteSpace(reason) || reason.Length < 10)
        {
            throw new InvalidOperationException(
                "Describe the problem (damage, wrong item, missing parts, or a lost package). Change of mind is not refundable after shipment.");
        }

        if (reason.Length > 500)
        {
            reason = reason[..500];
        }

        var tracking = request.ReturnTrackingNumber?.Trim();
        if (string.IsNullOrWhiteSpace(tracking))
        {
            tracking = item.ReturnTrackingNumber;
        }

        item.Status = RefundRequestedStatus;
        item.RefundRequestedAt ??= DateTime.UtcNow;
        item.ReturnTrackingNumber = string.IsNullOrWhiteSpace(tracking) ? null : tracking;
        item.RefundRequestReason = reason;
        _storeUnitOfWork.Repository<OrderItem>().Update(item);

        if (string.IsNullOrWhiteSpace(order.ReturnTrackingNumber))
        {
            order.ReturnTrackingNumber = tracking;
        }

        _storeUnitOfWork.Repository<Order>().Update(order);
        await _storeUnitOfWork.Complete();
        return MapToDto(order);
    }

    /// <summary>Public guest verify: token hash, unexpired, email, and ZIP must all match.</summary>
    public async Task<OrderDto?> VerifyGuestAccessAsync(GuestOrderAccessRequest request)
    {
        var order = await AuthenticateGuestOrderAsync(request);
        return order is null ? null : MapToDto(order);
    }

    /// <summary>Guest full-order cancel after the same token + email + ZIP check.</summary>
    public async Task<OrderDto?> RequestGuestCancellationAsync(GuestOrderAccessRequest request)
    {
        var order = await AuthenticateGuestOrderAsync(request);
        if (order is null) return null;
        return await RequestCancellationAsync(order.OrderId);
    }

    /// <summary>Guest line cancel after the same token + email + ZIP check.</summary>
    public async Task<OrderDto?> CancelGuestOrderItemAsync(GuestOrderAccessRequest request, int itemId)
    {
        var order = await AuthenticateGuestOrderAsync(request);
        if (order is null) return null;
        return await CancelOrderItemAsync(order.OrderId, itemId);
    }

    /// <summary>Guest per-item refund request after the same token + email + ZIP check.</summary>
    public async Task<OrderDto?> RequestGuestItemRefundAsync(GuestRefundRequest request, int itemId)
    {
        var order = await AuthenticateGuestOrderAsync(request);
        if (order is null) return null;
        return await RequestItemRefundAsync(
            order.OrderId,
            itemId,
            userId: null,
            new RequestRefundRequest
            {
                ReturnTrackingNumber = request.ReturnTrackingNumber,
                Reason = request.Reason
            });
    }

    /// <summary>Refunds the Stripe PaymentIntent, then marks the order refunded and restocks when it never shipped.</summary>
    public async Task<OrderDto?> RefundOrderAsync(string orderId)
    {
        var order = await LoadOrderAsync(orderId);
        if (order is null) return null;

        if (IsLocallyRefunded(order))
        {
            await InvalidateGuestAccessTokenAsync(order);
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

        if (RequiresReturnReceipt(order))
        {
            var dispute = await LoadLiveDisputeAsync(order);
            if (dispute is not null)
            {
                ApplyDispute(order, dispute);
                _storeUnitOfWork.Repository<Order>().Update(order);
                await _storeUnitOfWork.Complete();
            }

            if (!order.ReturnReceived)
            {
                throw new InvalidOperationException(ReturnReceiptRequiredMessage);
            }
        }
        else
        {
            await TryCancelCjShipmentAsync(order);
        }

        if (await TryRefundRequestedItemsAsync(order))
        {
            return MapToDto(order);
        }

        var refundedCents = await _paymentService.RefundPaymentAsync(order.PaymentTransactionId);
        await OnGuestStripeRefundSucceededAsync(order, refundedCents);
        await ApplyLocalRefundAsync(order);
        return MapToDto(order);
    }

    /// <summary>
    /// Opens a CJ refund dispute for a shipped order. Stripe refund stays locked until CJ confirms receipt.
    /// </summary>
    public async Task<OrderDto?> OpenReturnDisputeAsync(string orderId, OpenReturnDisputeRequest request)
    {
        var order = await LoadOrderAsync(orderId);
        if (order is null) return null;
        request ??= new OpenReturnDisputeRequest();

        if (IsLocallyRefunded(order) || order.Status == nameof(Status.Cancelled))
        {
            throw new InvalidOperationException("This order is already closed, so a return dispute is not needed.");
        }

        if (string.IsNullOrWhiteSpace(order.CjShipmentOrderId))
        {
            throw new InvalidOperationException("Submit this order to CJ before opening a return dispute.");
        }

        var tracking = request.ReturnTrackingNumber?.Trim();
        if (string.IsNullOrWhiteSpace(tracking))
        {
            tracking = order.Items
                .Where(item => !string.IsNullOrWhiteSpace(item.ReturnTrackingNumber))
                .OrderByDescending(item => item.RefundRequestedAt)
                .Select(item => item.ReturnTrackingNumber!.Trim())
                .FirstOrDefault();
        }

        if (string.IsNullOrWhiteSpace(tracking))
        {
            throw new InvalidOperationException("Enter the customer return tracking number first.");
        }

        order.ReturnTrackingNumber = tracking;

        var existing = await LoadLiveDisputeAsync(order);
        if (existing is not null && existing.FinallyDeal != 3)
        {
            ApplyDispute(order, existing);
            _storeUnitOfWork.Repository<Order>().Update(order);
            await _storeUnitOfWork.Complete();
            return MapToDto(order, existing, revealShippingAddress: true);
        }

        var products = await LoadDisputeProductsAsync(order.CjShipmentOrderId);
        var confirm = await _cjService.ConfirmDisputeInfoAsync(order.CjShipmentOrderId, products);
        if (confirm is null || confirm.Reasons.Count == 0)
        {
            throw new InvalidOperationException(
                "CJ did not return a dispute reason for this shipment. Confirm the order was created through the API.");
        }

        var confirmedProducts = confirm.Products.Count > 0
            ? confirm.Products.Where(product => !string.IsNullOrWhiteSpace(product.LineItemId)).ToList()
            : products;
        if (confirmedProducts.Count == 0)
        {
            confirmedProducts = products;
        }

        var reason = PickReturnReason(confirm.Reasons);
        if (reason is null)
        {
            throw new InvalidOperationException("CJ did not offer a return dispute reason for this shipment.");
        }

        var note = string.IsNullOrWhiteSpace(request.Message)
            ? $"Customer return. Tracking: {tracking}."
            : $"Customer return. Tracking: {tracking}. {request.Message.Trim()}";
        if (note.Length > 500)
        {
            note = note[..500];
        }

        var createError = await _cjService.CreateDisputeAsync(new CjCreateDisputeRequest
        {
            OrderId = order.CjShipmentOrderId,
            BusinessDisputeId = $"{order.OrderId}-RET-{DateTime.UtcNow:yyyyMMddHHmmss}",
            DisputeReasonId = reason.DisputeReasonId,
            ExpectType = 1,
            RefundType = 1,
            MessageText = note,
            Products = confirmedProducts
        });

        if (!string.IsNullOrWhiteSpace(createError))
        {
            _storeUnitOfWork.Repository<Order>().Update(order);
            await _storeUnitOfWork.Complete();
            throw new InvalidOperationException(createError);
        }

        order.CjDisputeStatus = "Processing";
        var dispute = await LoadLiveDisputeAsync(order) ?? new CjDisputeDto
        {
            Status = "Processing",
            DisputeReason = reason.ReasonName
        };
        ApplyDispute(order, dispute);
        _storeUnitOfWork.Repository<Order>().Update(order);
        await _storeUnitOfWork.Complete();
        return MapToDto(order, dispute, revealShippingAddress: true);
    }

    /// <summary>Asks CJ whether the return was received and whether the customer now qualifies for a refund.</summary>
    public async Task<OrderDto?> RefreshReturnDisputeAsync(string orderId)
    {
        var order = await LoadOrderAsync(orderId);
        if (order is null) return null;

        if (string.IsNullOrWhiteSpace(order.CjShipmentOrderId) && string.IsNullOrWhiteSpace(order.CjDisputeId))
        {
            return MapToDto(order, revealShippingAddress: true);
        }

        var dispute = await LoadLiveDisputeAsync(order);
        if (dispute is not null)
        {
            ApplyDispute(order, dispute);
            _storeUnitOfWork.Repository<Order>().Update(order);
            await _storeUnitOfWork.Complete();
        }

        return MapToDto(order, dispute, revealShippingAddress: true);
    }

    /// <summary>Marks the order refunded after Stripe already returned the money (Dashboard or webhook).</summary>
    public async Task<OrderDto?> ApplyPaymentRefundedAsync(string paymentIntentId)
    {
        var order = await _storeUnitOfWork.Repository<Order>()
            .GetFirstOrDefault(o => o.PaymentTransactionId == paymentIntentId, includeProperties: "Items");

        if (order is null) return null;
        if (IsLocallyRefunded(order))
        {
            await InvalidateGuestAccessTokenAsync(order);
            return MapToDto(order);
        }

        var refundedCents = PaymentService.ToMinorUnits(RemainingTotal(order));
        await OnGuestStripeRefundSucceededAsync(order, refundedCents);
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

    /// <summary>
    /// When a return is received and only some lines requested a refund, refund those Stripe line totals.
    /// Returns true when the caller should skip a full remaining-balance refund.
    /// </summary>
    private async Task<bool> TryRefundRequestedItemsAsync(Order order)
    {
        var remainingActive = order.Items.Where(IsActiveItem).ToList();
        var requested = remainingActive.Where(HasRefundRequest).ToList();
        if (requested.Count == 0)
        {
            return false;
        }

        if (requested.Count == remainingActive.Count)
        {
            return false;
        }

        var refundAmount = requested.Sum(item => item.PriceAtPurchase * item.Quantity);
        var refundedCents = await _paymentService.RefundPaymentAsync(
            order.PaymentTransactionId,
            PaymentService.ToMinorUnits(refundAmount),
            $"order-requested-refund-{order.OrderId}");
        await OnGuestStripeRefundSucceededAsync(order, refundedCents);

        foreach (var item in requested)
        {
            item.Status = Status.Refunded.ToString();
            _storeUnitOfWork.Repository<OrderItem>().Update(item);
        }

        order.LastStatusSyncAt = DateTime.UtcNow;
        _storeUnitOfWork.Repository<Order>().Update(order);
        await _storeUnitOfWork.Complete();
        return true;
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

    /// <summary>Orders older than this cutoff are hidden from customers and deleted.</summary>
    private static DateTime RetentionCutoffUtc() => DateTime.UtcNow.AddMonths(-3);

    /// <summary>Pages through matching orders so retention and history are not capped at one page.</summary>
    private async Task<List<Order>> CollectOrdersAsync(
        Expression<Func<Order, bool>>? filter,
        string? includeProperties = "Items")
    {
        var collected = new List<Order>();
        var page = 1;

        while (true)
        {
            var batch = await _storeUnitOfWork.Repository<Order>()
                .GetAllParams(
                    new PageParams { PageNumber = page, PageSize = 50 },
                    filter: filter,
                    orderby: query => query.OrderByDescending(order => order.CreatedAt),
                    includeProperties: includeProperties);

            collected.AddRange(batch);
            if (!batch.HasNextPage) break;
            page++;
        }

        return collected;
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
        var markup = await _storeSettingsService.GetDefaultMarkupAsync();
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

        var productType = await _storeUnitOfWork.Repository<ProductType>()
            .GetFirstOrDefault(type => type.Sku == sku);
        if (productType is not null)
        {
            return await _storeUnitOfWork.Repository<Products>()
                .GetFirstOrDefault(p => p.Id == productType.ProductId);
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

    /// <summary>Uses the type price when the line SKU matches a priced product type; otherwise the product sell price.</summary>
    private async Task<decimal> ResolveLinePriceAsync(Products product, string sku)
    {
        var productType = await _storeUnitOfWork.Repository<ProductType>()
            .GetFirstOrDefault(type => type.ProductId == product.Id && type.Sku == sku);
        return productType is { Price: > 0 } ? productType.Price : product.SellPrice;
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

    private const string ReturnReceiptRequiredMessage =
        "This order has shipped. Open a CJ return dispute and wait until CJ confirms the merchandise was received before issuing a Stripe refund.";

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
                : "<div><p>Thanks for your order {{ORDER_ID}}.</p>{{ITEMS}}<p>Total {{TOTAL}}</p>{{MANAGE_BLOCK}}</div>";

            var itemRows = string.Concat(order.Items.Select(item =>
            {
                var name = WebUtility.HtmlEncode(string.IsNullOrWhiteSpace(item.Name) ? item.Sku : item.Name);
                var lineTotal = (item.PriceAtPurchase * item.Quantity).ToString("0.00", System.Globalization.CultureInfo.InvariantCulture);
                return $"<tr><td style=\"padding:10px 0;border-bottom:1px solid #e2e8f0;color:#0b1f3a;\">{name}<br/><span style=\"color:#64748b;font-size:13px;\">{WebUtility.HtmlEncode(item.Sku)} × {item.Quantity}</span></td><td style=\"padding:10px 0;border-bottom:1px solid #e2e8f0;text-align:right;color:#0b1f3a;\">${lineTotal}</td></tr>";
            }));

            html = html
                .Replace("{{NAME}}", WebUtility.HtmlEncode(order.CustomerName))
                .Replace("{{EMAIL}}", WebUtility.HtmlEncode(order.CustomerEmail))
                .Replace("{{ORDER_ID}}", WebUtility.HtmlEncode(order.OrderId))
                .Replace("{{ITEMS}}", itemRows)
                .Replace("{{ADDRESS}}", WebUtility.HtmlEncode($"{order.ShippingAddress}, {order.City}, {order.State} {order.ZipCode} {order.Country}"))
                .Replace("{{SHIPPING}}", $"{WebUtility.HtmlEncode(order.LogisticName)} — ${order.ShippingCost.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture)}")
                .Replace("{{TOTAL}}", $"${order.Total.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture)}")
                .Replace("{{MANAGE_BLOCK}}", BuildManageEmailBlock(order.GuestManageUrl))
                .Replace("{{LOGO_URL}}", StoreUrls.LogoUrl(_configuration));

            await _emailSender.SendEmailAsync(order.CustomerEmail, $"Your CosmicStore order {order.OrderId[..Math.Min(8, order.OrderId.Length)]}", html);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send order confirmation for {OrderId}.", order.OrderId);
        }
    }

    private static bool HasGuestManageToken(Order order) =>
        !string.IsNullOrWhiteSpace(order.GuestAccessTokenHash);

    /// <summary>Clears the hashed guest manage token immediately so the email link cannot be reused.</summary>
    private async Task InvalidateGuestAccessTokenAsync(Order order)
    {
        if (!HasGuestManageToken(order))
        {
            return;
        }

        order.GuestAccessTokenHash = null;
        order.GuestAccessTokenExpiresAt = DateTime.UtcNow;
        _storeUnitOfWork.Repository<Order>().Update(order);
        await _storeUnitOfWork.Complete();
    }

    /// <summary>
    /// After Stripe accepts a refund, persist token invalidation first, then email the guest
    /// the cancelled amount and the 3-5 business day timeline.
    /// </summary>
    private async Task OnGuestStripeRefundSucceededAsync(Order order, long refundedCents)
    {
        if (!HasGuestManageToken(order))
        {
            return;
        }

        await InvalidateGuestAccessTokenAsync(order);

        if (refundedCents > 0)
        {
            await TrySendGuestCancellationEmailAsync(order, refundedCents);
        }
    }

    private async Task TrySendGuestCancellationEmailAsync(Order order, long refundedCents)
    {
        if (string.IsNullOrWhiteSpace(order.CustomerEmail))
        {
            return;
        }

        try
        {
            var filePath = Path.Combine(_env.WebRootPath ?? string.Empty, "templates", "GuestCancellation.html");
            var amount = PaymentService.FromMinorUnits(refundedCents)
                .ToString("0.00", System.Globalization.CultureInfo.InvariantCulture);
            var shortId = order.OrderId[..Math.Min(8, order.OrderId.Length)];
            var html = File.Exists(filePath)
                ? await File.ReadAllTextAsync(filePath)
                : "<div><p>Your order {{ORDER_ID}} was cancelled. We refunded ${{AMOUNT}}. Refunds typically appear in 3-5 business days.</p></div>";

            html = html
                .Replace("{{NAME}}", WebUtility.HtmlEncode(order.CustomerName))
                .Replace("{{EMAIL}}", WebUtility.HtmlEncode(order.CustomerEmail))
                .Replace("{{ORDER_ID}}", WebUtility.HtmlEncode(order.OrderId))
                .Replace("{{SHORT_ID}}", WebUtility.HtmlEncode(shortId))
                .Replace("{{AMOUNT}}", amount)
                .Replace("{{LOGO_URL}}", StoreUrls.LogoUrl(_configuration));

            await _emailSender.SendEmailAsync(
                order.CustomerEmail,
                $"Your CosmicStore order {shortId} was cancelled",
                html);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send guest cancellation email for {OrderId}.", order.OrderId);
        }
    }

    private string? BuildGuestManageUrl(string orderId, string token)
    {
        var baseUrl = StoreUrls.Absolute(_configuration, "ReturnPath:manageOrder", "/order/manage");
        return $"{baseUrl}?orderId={Uri.EscapeDataString(orderId)}&token={Uri.EscapeDataString(token)}";
    }

    private static string BuildManageEmailBlock(string? manageUrl)
    {
        if (string.IsNullOrWhiteSpace(manageUrl))
        {
            return string.Empty;
        }

        var safeUrl = WebUtility.HtmlEncode(manageUrl);
        return $"""
            <p style="color:#475569;font-size:15px;line-height:22px;margin:24px 0 0;">
                Need to cancel or request a refund? Use this private link, then confirm with the email from checkout (and the ZIP for a standard order):
            </p>
            <p style="margin:16px 0 0;">
                <a href="{safeUrl}" style="display:inline-block;background:#c45c12;color:#ffffff;text-decoration:none;padding:12px 20px;border-radius:8px;font-weight:600;">Manage this order</a>
            </p>
            """;
    }

    private async Task<Order?> AuthenticateGuestOrderAsync(GuestOrderAccessRequest? request)
    {
        if (request is null
            || string.IsNullOrWhiteSpace(request.OrderId)
            || string.IsNullOrWhiteSpace(request.Token)
            || string.IsNullOrWhiteSpace(request.Email)
            || string.IsNullOrWhiteSpace(request.ZipCode))
        {
            return null;
        }

        var order = await LoadOrderAsync(request.OrderId.Trim());
        if (order is null
            || string.IsNullOrWhiteSpace(order.GuestAccessTokenHash)
            || order.GuestAccessTokenExpiresAt is null
            || order.GuestAccessTokenExpiresAt.Value < DateTime.UtcNow)
        {
            return null;
        }

        if (!GuestTokensMatch(request.Token, order.GuestAccessTokenHash))
        {
            return null;
        }

        if (!string.Equals(NormalizeEmail(order.CustomerEmail), NormalizeEmail(request.Email), StringComparison.Ordinal))
        {
            return null;
        }

        // Registry buyers never receive the destination ZIP, so token + email are enough.
        if (order.WishlistId is null
            && !string.Equals(NormalizeZip(order.ZipCode), NormalizeZip(request.ZipCode), StringComparison.Ordinal))
        {
            return null;
        }

        return order;
    }

    private static string HashGuestToken(string token) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token.Trim())));

    private static bool GuestTokensMatch(string provided, string storedHash)
    {
        var providedHash = HashGuestToken(provided);
        var providedBytes = Encoding.UTF8.GetBytes(providedHash);
        var storedBytes = Encoding.UTF8.GetBytes(storedHash);
        return providedBytes.Length == storedBytes.Length
            && CryptographicOperations.FixedTimeEquals(providedBytes, storedBytes);
    }

    private static string NormalizeEmail(string? email) => (email ?? string.Empty).Trim().ToUpperInvariant();

    private static string NormalizeZip(string? zip)
    {
        if (string.IsNullOrWhiteSpace(zip))
        {
            return string.Empty;
        }

        return string.Concat(zip.Where(char.IsLetterOrDigit)).ToUpperInvariant();
    }

    /// <summary>Projects an Order entity into the API payload, including computed total.</summary>
    private static OrderDto MapToDto(Order order, CjDisputeDto? dispute = null, bool revealShippingAddress = false)
    {
        var liveDispute = dispute ?? SnapshotDispute(order);
        var maskedLabel = order.WishlistId is int
            ? WishlistPrivacy.MaskedShippingLabel(order.CustomerName)
            : null;
        var hideAddress = maskedLabel is not null && !revealShippingAddress;

        return new OrderDto
        {
            OrderId = order.OrderId,
            CjShipmentOrderId = order.CjShipmentOrderId,
            Status = order.Status,
            CustomerName = hideAddress ? WishlistPrivacy.FirstName(order.CustomerName) : order.CustomerName,
            CustomerEmail = order.CustomerEmail,
            ShippingAddress = hideAddress ? maskedLabel! : order.ShippingAddress,
            City = hideAddress ? string.Empty : order.City,
            State = hideAddress ? string.Empty : order.State,
            ZipCode = hideAddress ? string.Empty : order.ZipCode,
            Country = hideAddress ? string.Empty : order.Country,
            PaymentStatus = order.PaymentStatus,
            LogisticName = order.LogisticName,
            ShippingCost = order.ShippingCost,
            TrackingNumber = order.TrackingNumber,
            ReturnTrackingNumber = order.ReturnTrackingNumber,
            CjDisputeId = order.CjDisputeId,
            CjDisputeStatus = order.CjDisputeStatus,
            ReturnReceived = order.ReturnReceived,
            RequiresReturnReceipt = RequiresReturnReceipt(order),
            Dispute = liveDispute,
            LastStatusSyncAt = order.LastStatusSyncAt,
            CreatedAt = order.CreatedAt,
            RefundWindowEndsAt = RefundWindowEnd(order),
            IsWithinRefundWindow = IsWithinRefundWindow(order),
            HasRefundRequest = order.Items.Any(HasRefundRequest),
            Total = RemainingTotal(order),
            WishlistId = order.WishlistId,
            MaskedShippingLabel = maskedLabel,
            AcceptedTermsAt = order.AcceptedTermsAt,
            AcceptedTermsVersion = order.AcceptedTermsVersion,
            NeedsFtcShipAttention = FtcShippingCompliance.NeedsAttention(order),
            DaysAwaitingFulfillment = FtcShippingCompliance.DaysAwaitingFulfillment(order),
            Items = order.Items.Select(i => new OrderItemDto
            {
                Id = i.Id,
                Sku = i.Sku,
                Name = i.Name,
                Status = string.IsNullOrWhiteSpace(i.Status) ? "Ordered" : i.Status,
                Quantity = i.Quantity,
                PriceAtPurchase = i.PriceAtPurchase,
                RefundRequestedAt = i.RefundRequestedAt,
                RefundRequestReason = i.RefundRequestReason,
                ReturnTrackingNumber = i.ReturnTrackingNumber,
                CanRequestRefund = CanCustomerRequestRefund(order, i),
                CanUpdateRefundRequest = CanCustomerUpdateRefundRequest(order, i)
            }).ToList()
        };
    }

    private static DateTime RefundWindowEnd(Order order) => order.CreatedAt.AddDays(RefundWindowDays);

    private static bool IsWithinRefundWindow(Order order) => DateTime.UtcNow <= RefundWindowEnd(order);

    private static bool HasRefundRequest(OrderItem item) =>
        item.RefundRequestedAt is not null
        || !string.IsNullOrWhiteSpace(item.ReturnTrackingNumber)
        || string.Equals(item.Status, RefundRequestedStatus, StringComparison.OrdinalIgnoreCase);

    private static bool CanCustomerRequestRefund(Order order, OrderItem item) =>
        IsWithinRefundWindow(order)
        && RequiresReturnReceipt(order)
        && IsActiveItem(item)
        && !HasRefundRequest(item)
        && order.PaymentStatus is nameof(Status.PaymentRecevied) or nameof(Status.Paid);

    private static bool CanCustomerUpdateRefundRequest(Order order, OrderItem item) =>
        IsWithinRefundWindow(order)
        && HasRefundRequest(item)
        && item.Status is not (nameof(Status.Cancelled) or nameof(Status.Refunded));

    /// <summary>Shipped CJ orders must wait for warehouse receipt before a customer refund.</summary>
    private static bool RequiresReturnReceipt(Order order) =>
        order.ReturnReceived
        || !string.IsNullOrWhiteSpace(order.CjDisputeId)
        || order.Status is nameof(Status.Shipped) or nameof(Status.Delivered)
        || (!string.IsNullOrWhiteSpace(order.CjShipmentOrderId) && !string.IsNullOrWhiteSpace(order.TrackingNumber));

    private static CjDisputeDto? SnapshotDispute(Order order)
    {
        if (string.IsNullOrWhiteSpace(order.CjDisputeId) && string.IsNullOrWhiteSpace(order.CjDisputeStatus))
        {
            return null;
        }

        return new CjDisputeDto
        {
            DisputeId = order.CjDisputeId,
            Status = order.CjDisputeStatus,
            ReturnReceived = order.ReturnReceived
        };
    }

    private async Task<CjDisputeDto?> LoadLiveDisputeAsync(Order order)
    {
        if (!string.IsNullOrWhiteSpace(order.CjDisputeId))
        {
            var detail = await _cjService.GetDisputeDetailAsync(order.CjDisputeId);
            if (detail is not null)
            {
                return detail;
            }
        }

        if (string.IsNullOrWhiteSpace(order.CjShipmentOrderId) && string.IsNullOrWhiteSpace(order.OrderId))
        {
            return null;
        }

        var disputes = await _cjService.GetDisputesAsync(order.CjShipmentOrderId, order.OrderId);
        return disputes.FirstOrDefault();
    }

    private static void ApplyDispute(Order order, CjDisputeDto dispute)
    {
        if (!string.IsNullOrWhiteSpace(dispute.DisputeId))
        {
            order.CjDisputeId = dispute.DisputeId;
        }

        if (!string.IsNullOrWhiteSpace(dispute.Status))
        {
            order.CjDisputeStatus = dispute.Status;
        }

        order.ReturnReceived = dispute.ReturnReceived;
        order.LastStatusSyncAt = DateTime.UtcNow;
    }

    private async Task<List<CjDisputeProduct>> LoadDisputeProductsAsync(string cjOrderId)
    {
        var products = (await _cjService.GetDisputeProductsAsync(cjOrderId))
            .Where(product => !string.IsNullOrWhiteSpace(product.LineItemId))
            .ToList();

        var choosable = products.Where(product => product.CanChoose).ToList();
        var selected = choosable.Count > 0 ? choosable : products;
        if (selected.Count == 0)
        {
            throw new InvalidOperationException("CJ has no dispute-eligible items for this shipment.");
        }

        foreach (var product in selected.Where(item => item.Quantity < 1))
        {
            product.Quantity = 1;
        }

        return selected;
    }

    private static CjDisputeReason? PickReturnReason(IEnumerable<CjDisputeReason> reasons)
    {
        var list = reasons.Where(reason => reason.DisputeReasonId > 0).ToList();
        return list.FirstOrDefault(reason => ContainsAny(reason.ReasonName, "return", "returned", "warehouse", "quality", "defective", "damaged", "not as described"))
            ?? list.FirstOrDefault(reason => !ContainsAny(reason.ReasonName, "unfulfilled", "cancel"))
            ?? list.FirstOrDefault();
    }

    private static bool ContainsAny(string? value, params string[] needles)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        return needles.Any(needle => value.Contains(needle, StringComparison.OrdinalIgnoreCase));
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
