using Models;

namespace Data.Interfaces;

public interface ICJDropshippingService
{
    Task<string> CreateOrderV3Async(CjCreateOrderV3Request requestPayload);
    Task<bool> PayBalanceV2Async(string shipmentOrderId);
}
