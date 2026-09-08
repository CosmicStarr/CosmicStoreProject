using Models;
using Models.AngularDTOs;

namespace Data.Interfaces;

public interface ICJDropshippingService
{
    Task<string> CreateOrderV3Async(CjCreateOrderV3Request requestPayload);
    Task<bool> PayBalanceV2Async(string shipmentOrderId);

    /// <summary>Quotes the available CJ logistics options for a destination and basket of variants.</summary>
    Task<IReadOnlyList<ShippingOptionDto>> GetFreightOptionsAsync(CjFreightRequest request);

    /// <summary>Reads the CJ wallet balance used to pay for fulfilment.</summary>
    Task<CjBalanceDto?> GetWalletBalanceAsync();

    /// <summary>Fetches the current fulfilment status and tracking number for a shipment order.</summary>
    Task<CjOrderStatusDto?> GetOrderStatusAsync(string shipmentOrderId);

    /// <summary>Requests cancellation of an unshipped CJ order.</summary>
    Task<bool> CancelOrderAsync(string shipmentOrderId);

    /// <summary>Lists the CJ variants (vid/sku pairs) belonging to a CJ product.</summary>
    Task<IReadOnlyList<CjVariantDto>> GetProductVariantsAsync(string cjProductId);

    /// <summary>Reads current CJ warehouse stock for the given variant ids.</summary>
    Task<IReadOnlyDictionary<string, int>> GetVariantStockAsync(IEnumerable<string> variantIds);
}
