using AutoMapper;
using Data;
using Data.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Models;
using Models.DTOs;

namespace CosmicStoreAPI.Controllers;


public class ProductsController(IUnitOfWork unitOfWork, IMapper mapper) : BaseController
{
    private readonly IUnitOfWork _unitOfWork = unitOfWork;

    private readonly IMapper _mapper = mapper;

    [HttpGet]
    public async Task<ActionResult<IEnumerable<ProductDTO>>> GetAllProducts()
    {
        var Data = await _unitOfWork.Repository<Products>().GetAll(null,x=>x.OrderBy(x=>x.Name),"Category,Brand");
        return Ok(_mapper.Map<IEnumerable<Products>,IEnumerable<ProductDTO>>(Data));
    }

    [HttpGet("{Id}")]
    public async Task<ActionResult<ProductDTO>> GetProduct(int Id)
    {
        var Data = await _unitOfWork.Repository<Products>().GetFirstOrDefault(x=>x.Id == Id,"Category,Brand");
        return Ok(_mapper.Map<Products,ProductDTO>(Data));
    }

}