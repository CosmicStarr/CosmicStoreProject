using Data.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Models;
using Models.AngularDTOs;

namespace CosmicStoreAPI.Controllers;

/// <summary>
/// Checkout shipping quotes from CJ freight, using each cart SKU's mapped vid.
/// </summary>
public class ShippingController(
    ICJDropshippingService cjService,
    IStoreUnitOfWork storeUnitOfWork,
    IWishlistRegistryService registryService,
    IConfiguration configuration) : BaseController
{
    private readonly ICJDropshippingService _cjService = cjService;
    private readonly IStoreUnitOfWork _storeUnitOfWork = storeUnitOfWork;
    private readonly IWishlistRegistryService _registryService = registryService;
    private readonly IConfiguration _configuration = configuration;

    /// <summary>
    /// Quotes CJ logistics options for a destination and basket. Falls back to a flat rate if CJ has no mapping.
    /// </summary>
    [HttpPost("quote")]
    public async Task<ActionResult<IEnumerable<ShippingOptionDto>>> GetQuote(ShippingQuoteRequest request)
    {
        if (request.Items.Count == 0)
        {
            return BadRequest(new { message = "At least one cart item is required for a shipping quote." });
        }

        var countryCode = request.CountryCode;
        var province = request.ProvinceOrState;
        var city = request.City;

        if (request.WishlistId is int wishlistId)
        {
            try
            {
                var registry = await _registryService.RequirePublicRegistryAsync(wishlistId);
                var address = registry.ShippingAddress!;
                countryCode = address.CountryCode;
                province = address.ProvinceOrState;
                city = address.City;
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        if (string.IsNullOrWhiteSpace(countryCode))
        {
            return BadRequest(new { message = "A destination country is required for a shipping quote." });
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
            countryCode = countryCode,
            province = province,
            city = city,
            products = freightProducts
        });

        return Ok(options.Count > 0 ? options : FallbackOptions());
    }

    /// <summary>
    /// Resolves a cart-line SKU to a CJ vid: local ProductVariant, then CJ by SKU, then product/image fallbacks.
    /// </summary>
    private async Task<string?> ResolveVariantIdAsync(string sku)
    {
        var variant = await _storeUnitOfWork.Repository<ProductVariant>()
            .GetFirstOrDefault(v => v.Sku == sku);

        if (variant is not null && !string.IsNullOrWhiteSpace(variant.CjVariantId))
        {
            return variant.CjVariantId;
        }

        var remoteVid = await _cjService.ResolveVidBySkuAsync(sku);
        if (!string.IsNullOrWhiteSpace(remoteVid))
        {
            await RememberVariantMappingAsync(sku, remoteVid, variant);
            return remoteVid;
        }

        var product = await _storeUnitOfWork.Repository<Products>()
            .GetFirstOrDefault(p => p.Sku == sku);
        if (product is not null)
        {
            return product.CjVariantId ?? product.Id;
        }

        var image = await _storeUnitOfWork.Repository<ProductImage>()
            .GetFirstOrDefault(img => img.SkuPhoto == sku);
        if (image is not null)
        {
            product = await _storeUnitOfWork.Repository<Products>()
                .GetFirstOrDefault(p => p.Id == image.ProductId);
            return product?.CjVariantId ?? product?.Id;
        }

        return null;
    }

    /// <summary>Stores a CJ vid found by SKU so later freight quotes stay local.</summary>
    private async Task RememberVariantMappingAsync(string sku, string vid, ProductVariant? existing)
    {
        if (existing is not null)
        {
            existing.CjVariantId = vid;
            existing.LastSyncedAt = DateTime.UtcNow;
            _storeUnitOfWork.Repository<ProductVariant>().Update(existing);
            await _storeUnitOfWork.Complete();
            return;
        }

        var product = await _storeUnitOfWork.Repository<Products>()
            .GetFirstOrDefault(p => p.Sku == sku);
        if (product is null)
        {
            var image = await _storeUnitOfWork.Repository<ProductImage>()
                .GetFirstOrDefault(img => img.SkuPhoto == sku);
            if (image is not null)
            {
                product = await _storeUnitOfWork.Repository<Products>()
                    .GetFirstOrDefault(p => p.Id == image.ProductId);
            }
        }

        if (product is null)
        {
            return;
        }

        _storeUnitOfWork.Repository<ProductVariant>().Add(new ProductVariant
        {
            ProductId = product.Id,
            CjVariantId = vid,
            Sku = sku,
            LastSyncedAt = DateTime.UtcNow
        });
        await _storeUnitOfWork.Complete();
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
                DeliveryTime = "10-21",
                FreightCost = flatRate
            }
        ];
    }
}
