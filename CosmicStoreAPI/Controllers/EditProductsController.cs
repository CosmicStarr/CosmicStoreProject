using Data.Interfaces;
using Data.Util;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Models;
using Models.AngularDTOs;

namespace CosmicStoreAPI.Controllers;

[Authorize(Roles = "Admin")]
public class EditProductsController(
    IUnitOfWork unitOfWork,
    IEditCjProducts editCjProducts,
    ICacheService cacheService,
    IConfiguration configuration) : BaseController
{
    private readonly IUnitOfWork _unitOfWork = unitOfWork;
    private readonly IEditCjProducts _editCjProducts = editCjProducts;
    private readonly ICacheService _cacheService = cacheService;
    private readonly IConfiguration _configuration = configuration;

    [HttpGet]
    public async Task<ActionResult<FlatProduct>> GetAllProducts([FromQuery] PageParams pageParams)
    {
        var product = await _unitOfWork.Repository<FlatProduct>().GetAllParams(pageParams, null, null, "Category");
        Response.AddPaginationHeader(product.CurrentPage, product.PageSize, product.TotalCount, product.TotalPages);
        return Ok(product);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<FlatProduct>> GetProduct(string id)
    {
        var info = await _unitOfWork.Repository<FlatProduct>().GetFirstOrDefault(x => x.Id == id, "Category");
        if (info is null) return NotFound();
        return Ok(info);
    }

    [HttpPost("UpdateProduct/{id}")]
    public async Task<ActionResult<Products>> EditProduct(string id, [FromBody] EditProductInfo product)
    {
        var data = await _editCjProducts.EditCjProductAsync(id, product);
        await InvalidateProductCacheAsync();
        return Ok(data);
    }

    [HttpPost("Publish/{id}")]
    public async Task<ActionResult<Products>> PublishProduct(string id, [FromQuery] decimal? markup)
    {
        var multiplier = markup ?? _configuration.GetValue("StoreSettings:DefaultMarkup", 1.4m);
        var data = await _editCjProducts.PublishFlatProductAsync(id, multiplier);
        await InvalidateProductCacheAsync();
        return Ok(data);
    }

    [HttpPost("PublishBulk")]
    public async Task<ActionResult<IReadOnlyList<Products>>> PublishBulk([FromBody] PublishBulkRequest request)
    {
        var multiplier = request.MarkupMultiplier > 0
            ? request.MarkupMultiplier
            : _configuration.GetValue("StoreSettings:DefaultMarkup", 1.4m);

        var data = await _editCjProducts.PublishBulkAsync(request.ProductIds, multiplier);
        await InvalidateProductCacheAsync();
        return Ok(data);
    }

    [HttpGet("settings")]
    public ActionResult<StoreSettingsDto> GetSettings()
    {
        return Ok(new StoreSettingsDto
        {
            DefaultMarkup = _configuration.GetValue("StoreSettings:DefaultMarkup", 1.4m)
        });
    }

    private async Task InvalidateProductCacheAsync()
    {
        await _cacheService.RemoveData("products_all");
    }
}
