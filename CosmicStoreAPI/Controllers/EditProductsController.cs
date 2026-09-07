using Data.Interfaces;
using Data.Util;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Build.Framework;
using Models;

namespace CosmicStoreAPI.Controllers;


public class EditProductsController(IUnitOfWork unitOfWork,IEditCjProducts editCjProducts):BaseController
{
    private readonly IUnitOfWork _unitOfWork = unitOfWork;
    private readonly IEditCjProducts _editCjProducts = editCjProducts;

    //Admin can get all products and edit them
    [HttpGet]
    public async Task<ActionResult<FlatProduct>> GetAllProducts([FromQuery]PageParams pageParams)
    {
        var product = await _unitOfWork.Repository<FlatProduct>().GetAllParams(pageParams,null,null,"Category");
        Response.AddPaginationHeader(product.CurrentPage, product.PageSize, product.TotalCount, product.TotalPages);
        return Ok(product);
    }
    //Admin can get a single product and edit it
    [HttpGet("{Id}")]
    public async Task<ActionResult<FlatProduct>> GetProduct(string id)
    {
        var info = await _unitOfWork.Repository<FlatProduct>().GetFirstOrDefault(x=>x.Id == id,"Category");

        return Ok(info);
    }

    //Admin can edit a product and update it in the database
    [HttpPost("UpdateProduct/{Id}")]
    public async Task<ActionResult<IEnumerable<Products>>> EditProduct(string id,[FromBody]EditProductInfo product)
    {
        Console.WriteLine($"Received product data: {product}");
        var Data = await _editCjProducts.EditCjProductAsync(id,product);
        return Ok(Data);
    }
    
}