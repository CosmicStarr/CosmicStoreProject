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
    public async Task<ActionResult<IEnumerable<ProductResponseDto>>> GetJoinedProducts(
    [FromQuery] PageParams pageParams)
    {
        // 1. Define a unique cache key based on the category
        string cacheKey = string.IsNullOrEmpty(pageParams.Category) 
            ? "products_all" 
            : $"products_category_{pageParams.Category.ToLower()}";

        if (pageParams.ClearCache)
        {
            await _cacheService.RemoveData(cacheKey);
        }
        // 2. Try to get the grouped data from Redis
        var groupedInfo = await _cacheService.GetCachedObject<List<ProductResponseDto>>(cacheKey);

        if (groupedInfo == null)
        {
            // Cache Miss: Fetch from SQL Database
            var categoryParam = new SqlParameter("@Category", (object?)pageParams.Category ?? DBNull.Value);
            var parameters = new object[] { categoryParam };
            
            // Execute the stored procedure to get the flat data
            var rawData = await _storeUnitOfWork.Repository<ProductWithPictureDto>()
                    .GetFromSqlAsync(SqlConstants.StoredProcedures.GetProductsWithPictures, parameters);
            
            // Group the flat data into nested structure        
            groupedInfo = _editCjProducts.GroupData(rawData).ToList();
            
            // Save to Redis for future requests (e.g., caching for 1 hour)
            if (groupedInfo.Any())
            {
                await _cacheService.ObjectToCache(cacheKey, groupedInfo, TimeSpan.FromHours(1));
            }
        }

        // 3. Apply search filter
        if (!string.IsNullOrEmpty(pageParams.Search))
        {
            groupedInfo = groupedInfo
                .Where(x => x.NameEn != null && x.NameEn.Contains(pageParams.Search, StringComparison.OrdinalIgnoreCase))
                .ToList(); 
        }

        // 4. Apply sorting
        groupedInfo = pageParams.Sort switch
        {
            "priceAsc" => groupedInfo.OrderBy(x => x.SellPrice).ToList(),
            "priceDesc" => groupedInfo.OrderByDescending(x => x.SellPrice).ToList(),
            "nameDesc" => groupedInfo.OrderByDescending(x => x.NameEn).ToList(),
            "newest" => groupedInfo.OrderByDescending(x => x.IsNewArrival).ThenBy(x => x.NameEn).ToList(),
            _ => groupedInfo.OrderBy(x => x.NameEn).ToList() // Default sorting (name Ascending)
        };

        // 5. Paginate the filtered and sorted data in-memory
        var paginatedData = PagerList<ProductResponseDto>.Create(groupedInfo, pageParams.PageNumber, pageParams.PageSize);

        // 6. Attach the Static Header
        Response.AddPaginationHeader(
            paginatedData.CurrentPage, 
            paginatedData.PageSize, 
            paginatedData.TotalCount, 
            paginatedData.TotalPages
        );

        // 7. Return the items
        return Ok(paginatedData);
    }


    [HttpGet("{id}")]
    public async Task<ActionResult<ProductWithPictureDto>>GetSingleProduct(string id)
    {
        var productIdParam = new SqlParameter("@ProductId", id);
        var parameters = new object[] { productIdParam };
        var rawData = await _storeUnitOfWork.Repository<ProductWithPictureDto>()
                .GetFromSqlAsync(SqlConstants.StoredProcedures.GetSingleProductWithPictures,parameters);
        var groupedInfo = _editCjProducts.GroupData(rawData);        
        var info = groupedInfo.FirstOrDefault();
        if(info == null)
        {
            return NotFound();
        }
        return Ok(info);
    }

}