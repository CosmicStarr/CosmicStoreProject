using CosmicStoreAPI.Error;
using Data;
using Data.Util;
using Data.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Models;
using Models.AngularDTOs;
using Microsoft.Data.SqlClient;

namespace CosmicStoreAPI.Controllers;

public class ProductsController(IStoreUnitOfWork storeUnitOfWork, IEditCjProducts editCjProducts, ICacheService cacheService) : BaseController
{
    private readonly IStoreUnitOfWork _storeUnitOfWork = storeUnitOfWork;
    private readonly IEditCjProducts _editCjProducts = editCjProducts;
    private readonly ICacheService _cacheService = cacheService;

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

    [HttpGet("{id}")]
    public async Task<ActionResult<ProductResponseDto>> GetSingleProduct(string id)
    {
        var productIdParam = new SqlParameter("@ProductId", id);
        var parameters = new object[] { productIdParam };
        var rawData = await _storeUnitOfWork.Repository<ProductWithPictureDto>()
            .GetFromSqlAsync(SqlConstants.StoredProcedures.GetSingleProductWithPictures, parameters);
        var groupedInfo = _editCjProducts.GroupData(rawData);
        var info = groupedInfo.FirstOrDefault();

        if (info is null)
        {
            return NotFound();
        }

        return Ok(info);
    }

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
            .Take(Math.Clamp(limit, 1, 12))
            .ToList();

        return Ok(related);
    }

    private async Task<List<ProductResponseDto>> LoadProductsAsync(string? category, bool clearCache)
    {
        var cacheKey = string.IsNullOrEmpty(category)
            ? "products_all"
            : $"products_category_{category.ToLower()}";

        if (clearCache)
        {
            await _cacheService.RemoveData(cacheKey);
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

    private static List<ProductResponseDto> ApplyFilters(List<ProductResponseDto> products, PageParams pageParams)
    {
        IEnumerable<ProductResponseDto> filtered = products;

        if (!string.IsNullOrWhiteSpace(pageParams.Search))
        {
            filtered = filtered.Where(x =>
                (!string.IsNullOrEmpty(x.NameEn) && x.NameEn.Contains(pageParams.Search, StringComparison.OrdinalIgnoreCase))
                || (!string.IsNullOrEmpty(x.Sku) && x.Sku.Contains(pageParams.Search, StringComparison.OrdinalIgnoreCase)));
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
