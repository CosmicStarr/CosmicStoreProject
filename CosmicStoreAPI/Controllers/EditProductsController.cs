using System.Linq.Expressions;
using Data.Classes;
using Data.Interfaces;
using Data.Util;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Models;
using Models.AngularDTOs;

namespace CosmicStoreAPI.Controllers;

/// <summary>
/// Admin catalog: browse CJ staging (FlatProducts), publish to the storefront, and create/edit store products.
/// </summary>
[Authorize(Roles = "Admin")]
public class EditProductsController(
    IUnitOfWork unitOfWork,
    IStoreUnitOfWork storeUnitOfWork,
    IEditCjProducts editCjProducts,
    ICacheService cacheService,
    IStoreSettingsService storeSettingsService) : BaseController
{
    private readonly IUnitOfWork _unitOfWork = unitOfWork;
    private readonly IStoreUnitOfWork _storeUnitOfWork = storeUnitOfWork;
    private readonly IEditCjProducts _editCjProducts = editCjProducts;
    private readonly ICacheService _cacheService = cacheService;
    private readonly IStoreSettingsService _storeSettingsService = storeSettingsService;

    /// <summary>
    /// Paged CJ staging catalog (dbo.FlatProducts) for the admin dashboard grid.
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<FlatProduct>> GetAllProducts([FromQuery] PageParams pageParams)
    {
        var search = pageParams.Search?.Trim();
        var category = pageParams.Category?.Trim();

        Expression<Func<FlatProduct, bool>>? filter = null;

        if (!string.IsNullOrWhiteSpace(search) && !string.IsNullOrWhiteSpace(category))
        {
            filter = p => (p.NameEn.Contains(search) || p.Sku.Contains(search))
                          && p.Category != null && p.Category.CategoryName == category;
        }
        else if (!string.IsNullOrWhiteSpace(search))
        {
            filter = p => p.NameEn.Contains(search) || p.Sku.Contains(search);
        }
        else if (!string.IsNullOrWhiteSpace(category))
        {
            filter = p => p.Category != null && p.Category.CategoryName == category;
        }

        // Paging without an ORDER BY lets SQL Server return rows in any order, which
        // makes products repeat or vanish as you page. Always sort by something stable.
        Func<IQueryable<FlatProduct>, IOrderedQueryable<FlatProduct>> orderBy = pageParams.Sort switch
        {
            "priceAsc" => q => q.OrderBy(p => p.SellPrice).ThenBy(p => p.Id),
            "priceDesc" => q => q.OrderByDescending(p => p.SellPrice).ThenBy(p => p.Id),
            "nameDesc" => q => q.OrderByDescending(p => p.NameEn).ThenBy(p => p.Id),
            _ => q => q.OrderBy(p => p.NameEn).ThenBy(p => p.Id)
        };

        var product = await _unitOfWork.Repository<FlatProduct>()
            .GetAllParams(pageParams, filter, orderBy, "Category");

        Response.AddPaginationHeader(product.CurrentPage, product.PageSize, product.TotalCount, product.TotalPages);
        return Ok(product);
    }

    /// <summary>
    /// Distinct category names from the CJ staging catalog, for the admin filter.
    /// </summary>
    [HttpGet("categories")]
    public async Task<ActionResult<IEnumerable<string>>> GetCategories()
    {
        var categories = await _unitOfWork.Repository<FlatCategory>()
            .GetAll(orderby: q => q.OrderBy(c => c.CategoryName));

        return Ok(categories
            .Select(c => c.CategoryName)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Distinct());
    }

    /// <summary>
    /// Loads one product for the edit form: storefront row plus <c>store.Pictures</c> when published,
    /// otherwise the <c>dbo.FlatProducts</c> staging row and any saved editor overlay.
    /// </summary>
    [HttpGet("{id}")]
    public async Task<ActionResult<ProductResponseDto>> GetProduct(string id)
    {
        var storeProduct = await _storeUnitOfWork.Repository<Products>()
            .GetFirstOrDefault(item => item.Id == id, "ProductImages.ProductType,ProductTypes");

        if (storeProduct is not null)
        {
            var response = EditCjProducts.ToResponse(storeProduct);
            var variants = await _storeUnitOfWork.Repository<ProductVariant>()
                .GetAllParams(new PageParams { PageNumber = 1, PageSize = 50 }, variant => variant.ProductId == id);
            EditCjProducts.MergeVariantPictures(response, variants, addMissingPictures: true);
            return Ok(response);
        }

        var staging = await _editCjProducts.GetStagingProductAsync(id);
        return staging is null ? NotFound() : Ok(staging);
    }

    /// <summary>
    /// Adds a product to <c>dbo.FlatProducts</c> for the admin dashboard. Publish later to put it on the storefront.
    /// </summary>
    [HttpPost("Create")]
    public async Task<ActionResult<ProductResponseDto>> CreateProduct([FromBody] EditProductInfo product)
    {
        try
        {
            var data = await _editCjProducts.CreateManualProductAsync(product);
            return Ok(data);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Removes a product from the storefront only (<c>store.GetProducts</c>). Keeps <c>dbo.FlatProducts</c>.
    /// </summary>
    [HttpPost("Unpublish/{id}")]
    public async Task<IActionResult> UnpublishProduct(string id)
    {
        var removed = await _editCjProducts.UnpublishStoreProductAsync(id);
        if (!removed)
        {
            return NotFound(new { message = "That product is not on the storefront." });
        }

        await InvalidateProductCacheAsync();
        return NoContent();
    }

    /// <summary>
    /// Deletes the product from the storefront (if published) and from <c>dbo.FlatProducts</c>.
    /// </summary>
    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteProduct(string id)
    {
        var deleted = await _editCjProducts.DeleteStoreProductAsync(id);
        if (!deleted)
        {
            return NotFound(new { message = "That product was not found in the catalog or storefront." });
        }

        await InvalidateProductCacheAsync();
        return NoContent();
    }

    /// <summary>
    /// Deletes one gallery image. Pass pictureId when known, or photoUrl (+ optional skuPhoto) as a fallback
    /// when several variants share the same image URL.
    /// </summary>
    [HttpDelete("{id}/images")]
    public async Task<ActionResult<ProductResponseDto>> DeleteProductImage(
        string id,
        [FromQuery] int? pictureId,
        [FromQuery] string? photoUrl,
        [FromQuery] string? skuPhoto = null)
    {
        if (pictureId is null or < 1 && string.IsNullOrWhiteSpace(photoUrl))
        {
            return BadRequest(new { message = "A picture id or image URL is required." });
        }

        var product = await _editCjProducts.DeleteProductImageAsync(id, pictureId, photoUrl, skuPhoto);
        if (product is not null)
        {
            await InvalidateProductCacheAsync();
            return Ok(EditCjProducts.ToResponse(product));
        }

        var staging = await _editCjProducts.DeleteStagingImageAsync(id, photoUrl, skuPhoto);
        return staging is null
            ? NotFound(new { message = "That image was not found on this product." })
            : Ok(staging);
    }

    /// <summary>
    /// Updates a published storefront product, or the staging catalog row when it is not published yet.
    /// </summary>
    [HttpPost("UpdateProduct/{id}")]
    public async Task<ActionResult<ProductResponseDto>> EditProduct(string id, [FromBody] EditProductInfo product)
    {
        var storeProduct = await _storeUnitOfWork.Repository<Products>()
            .GetFirstOrDefault(item => item.Id == id);

        if (storeProduct is null)
        {
            var staging = await _editCjProducts.UpdateStagingProductAsync(id, product);
            return staging is null ? NotFound(new { message = "That catalog product was not found." }) : Ok(staging);
        }

        var data = await _editCjProducts.EditCjProductAsync(id, product);
        await InvalidateProductCacheAsync();
        return Ok(EditCjProducts.ToResponse(data));
    }

    /// <summary>
    /// Copies one FlatProduct into store.Products with markup, optionally overlaying editor fields.
    /// </summary>
    [HttpPost("Publish/{id}")]
    public async Task<ActionResult<Products>> PublishProduct(string id, [FromQuery] decimal? markup, [FromBody] EditProductInfo? overlay)
    {
        var multiplier = markup ?? await _storeSettingsService.GetDefaultMarkupAsync();
        var data = await _editCjProducts.PublishFlatProductAsync(id, multiplier, overlay);
        await InvalidateProductCacheAsync();
        return Ok(EditCjProducts.ToResponse(data));
    }

    /// <summary>
    /// Publishes many staging products to the storefront in one request.
    /// </summary>
    [HttpPost("PublishBulk")]
    public async Task<ActionResult<IReadOnlyList<Products>>> PublishBulk([FromBody] PublishBulkRequest request)
    {
        var multiplier = request.MarkupMultiplier > 0
            ? request.MarkupMultiplier
            : await _storeSettingsService.GetDefaultMarkupAsync();

        var data = await _editCjProducts.PublishBulkAsync(request.ProductIds, multiplier);
        await InvalidateProductCacheAsync();
        return Ok(data.Select(EditCjProducts.ToResponse).ToList());
    }

    /// <summary>
    /// Returns runtime store settings (markup, catalog sync interval) plus CJ sync last-run times.
    /// </summary>
    [HttpGet("settings")]
    public async Task<ActionResult<StoreRuntimeSettingsDto>> GetSettings()
    {
        return Ok(await _storeSettingsService.GetSettingsAsync());
    }

    /// <summary>
    /// Updates publish markup and catalog sync interval (6 or 12 hours).
    /// </summary>
    [HttpPut("settings")]
    public async Task<ActionResult<StoreRuntimeSettingsDto>> UpdateSettings([FromBody] UpdateStoreRuntimeSettingsRequest request)
    {
        try
        {
            return Ok(await _storeSettingsService.UpdateSettingsAsync(request));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>Drops cached storefront product lists so publish/create/edit show up immediately.</summary>
    private async Task InvalidateProductCacheAsync()
    {
        await _cacheService.RemoveData("products_all");
        await _cacheService.RemoveByPrefixAsync("products_category_");
    }
}
