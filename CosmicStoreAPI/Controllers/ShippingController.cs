using Data.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Models;
using Models.AngularDTOs;

namespace CosmicStoreAPI.Controllers;

public class ShippingController(
    ICJDropshippingService cjService,
    IStoreUnitOfWork storeUnitOfWork,
    IConfiguration configuration) : BaseController
{
    private readonly ICJDropshippingService _cjService = cjService;
    private readonly IStoreUnitOfWork _storeUnitOfWork = storeUnitOfWork;
    private readonly IConfiguration _configuration = configuration;

    [HttpPost("quote")]
    public async Task<ActionResult<IEnumerable<ShippingOptionDto>>> GetQuote(ShippingQuoteRequest request)
    {
        if (request.Items.Count == 0)
        {
            return BadRequest(new { message = "At least one cart item is required for a shipping quote." });
        }

        var freightProducts = new List<CjFreightProduct>();

        foreach (var item in request.Items)
        {
            var vid = await ResolveVariantIdAsync(item.Sku);
            if (vid is null) continue;

            freightProducts.Add(new CjFreightProduct { vid = vid, quantity = Math.Max(1, item.Amount) });
        }

        if (freightProducts.Count == 0)
        {
            return Ok(FallbackOptions());
        }

        var options = await _cjService.GetFreightOptionsAsync(new CjFreightRequest
        {
            startCountryCode = _configuration["CJDropshipping:FromCountryCode"] ?? "CN",
            countryCode = request.CountryCode,
            province = request.ProvinceOrState,
            city = request.City,
            products = freightProducts
        });

        return Ok(options.Count > 0 ? options : FallbackOptions());
    }

    private async Task<string?> ResolveVariantIdAsync(string sku)
    {
        var variant = await _storeUnitOfWork.Repository<ProductVariant>()
            .GetFirstOrDefault(v => v.Sku == sku);

        if (variant is not null && !string.IsNullOrWhiteSpace(variant.CjVariantId))
        {
            return variant.CjVariantId;
        }

        var product = await _storeUnitOfWork.Repository<Products>()
            .GetFirstOrDefault(p => p.Sku == sku);

        return product?.CjVariantId ?? product?.Id;
    }

    /// <summary>Keeps checkout usable when CJ is unreachable or the variants are not mapped yet.</summary>
    private List<ShippingOptionDto> FallbackOptions()
    {
        var defaultLogistic = _configuration["CJDropshipping:DefaultLogisticName"] ?? "CJPacket Ordinary";
        var flatRate = _configuration.GetValue("CJDropshipping:FallbackShippingCost", 4.99m);

        return
        [
            new ShippingOptionDto
            {
                LogisticName = defaultLogistic,
                DeliveryTime = "7-15",
                FreightCost = flatRate
            }
        ];
    }
}
