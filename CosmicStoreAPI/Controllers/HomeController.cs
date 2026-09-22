using Data.Classes;
using Data.Interfaces;
using Data.Util;
using Microsoft.AspNetCore.Mvc;
using Models;
using Models.AngularDTOs;

namespace CosmicStoreAPI.Controllers;

/// <summary>
/// Home-page storefront reads: highlighted product strips and a single product card.
/// </summary>
public class HomeController(IStoreUnitOfWork storeUnitOfWork, IEditCjProducts editCjProducts) : BaseController
{
    private readonly IStoreUnitOfWork _storeUnitOfWork = storeUnitOfWork;
    private readonly IEditCjProducts _editCjProducts = editCjProducts;

    /// <summary>
    /// Returns featured, new-arrival, or top-selling storefront products for the home sections.
    /// Featured falls back to the full catalog when nothing is flagged yet.
    /// </summary>
    [HttpGet("highlighted/{highlightType}")]
    public async Task<ActionResult<IEnumerable<ProductResponseDto>>> GetHighlightedProducts(string highlightType)
    {
        await _editCjProducts.ExpireStaleNewArrivalsAsync();

        System.Linq.Expressions.Expression<Func<Products, bool>>? filter = highlightType.ToLowerInvariant() switch
        {
            "featured" => product => product.IsFeatured,
            "newarrival" => product => product.IsNewArrival,
            "topselling" => product => product.IsTopSelling,
            _ => null
        };

        var pageParams = new PageParams { PageNumber = 1, PageSize = 48 };
        var products = await _storeUnitOfWork.Repository<Products>()
            .GetAllParams(pageParams, filter, query => query.OrderBy(product => product.NameEn), "ProductImages");

        // A brand-new storefront often has published products that were never flagged.
        // Featured is the home catalog, so show those rather than an empty section.
        if (products.Count == 0 && string.Equals(highlightType, "Featured", StringComparison.OrdinalIgnoreCase))
        {
            products = await _storeUnitOfWork.Repository<Products>()
                .GetAllParams(pageParams, null, query => query.OrderBy(product => product.NameEn), "ProductImages");
        }

        return Ok(products.Select(EditCjProducts.ToResponse));
    }

    /// <summary>
    /// Loads one published storefront product (with gallery images) by id.
    /// </summary>
    [HttpGet("{id}")]
    public async Task<ActionResult<ProductResponseDto>> GetSingleProduct(string id)
    {
        var product = await _storeUnitOfWork.Repository<Products>()
            .GetFirstOrDefault(item => item.Id == id, "ProductImages");

        if (product is null)
        {
            return NotFound();
        }

        return Ok(EditCjProducts.ToResponse(product));
    }
}
