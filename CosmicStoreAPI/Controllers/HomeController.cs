using Data.Util;
using Data.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Models.AngularDTOs;
using Microsoft.Data.SqlClient;

namespace CosmicStoreAPI.Controllers;



public class HomeController(IStoreUnitOfWork storeUnitOfWork,IEditCjProducts editCjProducts):BaseController
{
    private readonly IStoreUnitOfWork _storeUnitOfWork = storeUnitOfWork;
    private readonly IEditCjProducts _editCjProducts = editCjProducts;
    

    [HttpGet("highlighted/{highlightType}")]
    public async Task<ActionResult<IEnumerable<ProductWithPictureDto>>> GetHighlightedProducts(string highlightType)
    {
        //highlightType can be "Featured", "NewArrival", "ALL" ,or "TopSelling"
        var highlightTypeParam = new SqlParameter("@HighlightType", highlightType);
        var parameters = new object[] { highlightTypeParam };
        var rawData = await _storeUnitOfWork.Repository<ProductWithPictureDto>()
                .GetFromSqlAsync(SqlConstants.StoredProcedures.GetHighlightedProducts, parameters);
        var groupedInfo = _editCjProducts.GroupData(rawData).ToList();
        Console.WriteLine($"Grouped Info Count: {groupedInfo.Count}");
        return Ok(groupedInfo);
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