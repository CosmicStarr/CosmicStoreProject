using CosmicStoreAPI.Error;
using Data.Classes;
using Data.Util;
using Data.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Models;
using Models.AngularDTOs;
using Microsoft.Data.SqlClient;

namespace CosmicStoreAPI.Controllers;

/// <summary>
/// Customer-facing catalog: cached product lists, categories, product detail, and related items.
/// </summary>
public class ProductsController(IStoreUnitOfWork storeUnitOfWork, IEditCjProducts editCjProducts, ICacheService cacheService) : BaseController
{
    private readonly IStoreUnitOfWork _storeUnitOfWork = storeUnitOfWork;
    private readonly IEditCjProducts _editCjProducts = editCjProducts;
    private readonly ICacheService _cacheService = cacheService;

    /// <summary>
    /// Returns the paginated storefront catalog. Stored-procedure rows are grouped into
    /// one <see cref="ProductResponseDto"/> per product (with a Pictures array).
    /// </summary>
    [HttpGet("joined-products")]
    public async Task<ActionResult<IEnumerable<ProductResponseDto>>> GetJoinedProducts([FromQuery] PageParams pageParams)
    {
        var groupedInfo = await LoadProductsAsync(pageParams.Category, pageParams.ClearCache);
        groupedInfo = ApplyFilters(groupedInfo, pageParams);

        var paginatedData = PagerList<ProductResponseDto>.Create(groupedInfo, pageParams.PageNumber, pageParams.PageSize);

        Response.AddPaginationHeader(
            paginatedData.CurrentPage,
            paginatedData.PageSize,
            paginatedData.TotalCount,
            paginatedData.TotalPages);

        return Ok(paginatedData);
    }

    /// <summary>
    /// Builds the storefront category filter from published products (name + product count).
    /// </summary>
    [HttpGet("categories")]
    public async Task<ActionResult<IEnumerable<CategorySummaryDto>>> GetCategories([FromQuery] bool clearCache = false)
    {
        var products = await LoadProductsAsync(null, clearCache);

        var categories = products
            .Where(p => !string.IsNullOrWhiteSpace(p.Category))
            .GroupBy(p => p.Category!)
            .Select(g => new CategorySummaryDto { Name = g.Key, Count = g.Count() })
            .OrderBy(c => c.Name)
            .ToList();

        return Ok(categories);
    }

    /// <summary>
    /// Returns one published product and its gallery for the product detail page.
    /// </summary>
    [HttpGet("{id}")]
    public async Task<ActionResult<ProductResponseDto>> GetSingleProduct(string id)
    {
        var productIdParam = new SqlParameter("@ProductId", id);
        var parameters = new object[] { productIdParam };
        var rawData = await _storeUnitOfWork.Repository<ProductWithPictureDto>()
            .GetFromSqlAsync(SqlConstants.StoredProcedures.GetSingleProductWithPictures, parameters);
        var response = _editCjProducts.GroupData(rawData).FirstOrDefault();

        if (response is null)
        {
            return NotFound();
        }

        return Ok(response);
    }

    /// <summary>
    /// Returns other products in the same category for the "related" strip on product detail.
    /// </summary>
    [HttpGet("{id}/related")]
    public async Task<ActionResult<IEnumerable<ProductResponseDto>>> GetRelatedProducts(string id, [FromQuery] int limit = 4)
    {
        var productIdParam = new SqlParameter("@ProductId", id);
        var parameters = new object[] { productIdParam };
        var rawData = await _storeUnitOfWork.Repository<ProductWithPictureDto>()
            .GetFromSqlAsync(SqlConstants.StoredProcedures.GetSingleProductWithPictures, parameters);
        var current = _editCjProducts.GroupData(rawData).FirstOrDefault();

        if (current is null || string.IsNullOrWhiteSpace(current.Category))
        {
            return Ok(Array.Empty<ProductResponseDto>());
        }

        var groupedInfo = await LoadProductsAsync(current.Category, false);
        var related = groupedInfo
            .Where(p => p.Id != id)
            .Take(Math.Clamp(limit, 1, 4))
            .ToList();

        return Ok(related);
    }

    /// <summary>
    /// Loads published products from Redis, or from the pictures stored procedure if the cache is empty.
    /// </summary>
    private async Task<List<ProductResponseDto>> LoadProductsAsync(string? category, bool clearCache)
    {
        var expired = await _editCjProducts.ExpireStaleNewArrivalsAsync();

        var cacheKey = string.IsNullOrEmpty(category)
            ? "products_all"
            : $"products_category_{category.ToLower()}";

        if (clearCache || expired > 0)
        {
            await _cacheService.RemoveData("products_all");
            await _cacheService.RemoveByPrefixAsync("products_category_");
        }

        var groupedInfo = await _cacheService.GetCachedObject<List<ProductResponseDto>>(cacheKey);

        if (groupedInfo is null)
        {
            var categoryParam = new SqlParameter("@Category", (object?)category ?? DBNull.Value);
            var parameters = new object[] { categoryParam };

            var rawData = await _storeUnitOfWork.Repository<ProductWithPictureDto>()
                .GetFromSqlAsync(SqlConstants.StoredProcedures.GetProductsWithPictures, parameters);

            groupedInfo = _editCjProducts.GroupData(rawData).ToList();

            if (groupedInfo.Count > 0)
            {
                await _cacheService.ObjectToCache(cacheKey, groupedInfo, TimeSpan.FromHours(1));
            }
        }

        return groupedInfo;
    }

    /// <summary>
    /// Applies search, price range, and sort to an in-memory catalog page.
    /// </summary>
    private static List<ProductResponseDto> ApplyFilters(List<ProductResponseDto> products, PageParams pageParams)
    {
        IEnumerable<ProductResponseDto> filtered = products;

        if (!string.IsNullOrWhiteSpace(pageParams.Search))
        {
            filtered = filtered.Where(x =>
                (!string.IsNullOrEmpty(x.NameEn) && x.NameEn.Contains(pageParams.Search, StringComparison.OrdinalIgnoreCase))
                || (!string.IsNullOrEmpty(x.Sku) && x.Sku.Contains(pageParams.Search, StringComparison.OrdinalIgnoreCase))
                || x.Types.Any(type =>
                    (!string.IsNullOrEmpty(type.Sku) && type.Sku.Contains(pageParams.Search, StringComparison.OrdinalIgnoreCase))
                    || (!string.IsNullOrEmpty(type.Name) && type.Name.Contains(pageParams.Search, StringComparison.OrdinalIgnoreCase))));
        }

        if (pageParams.MinPrice.HasValue)
        {
            filtered = filtered.Where(x => x.SellPrice >= pageParams.MinPrice.Value);
        }

        if (pageParams.MaxPrice.HasValue)
        {
            filtered = filtered.Where(x => x.SellPrice <= pageParams.MaxPrice.Value);
        }

        return pageParams.Sort switch
        {
            "priceAsc" => filtered.OrderBy(x => x.SellPrice).ToList(),
            "priceDesc" => filtered.OrderByDescending(x => x.SellPrice).ToList(),
            "nameDesc" => filtered.OrderByDescending(x => x.NameEn).ToList(),
            "newest" => filtered.OrderByDescending(x => x.IsNewArrival).ThenBy(x => x.NameEn).ToList(),
            _ => filtered.OrderBy(x => x.NameEn).ToList()
        };
    }
}
