using Data.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Models.AngularDTOs;

namespace CosmicStoreAPI.Controllers;

[Authorize(Roles = "Admin")]
public class AdminOrdersController(IOrderService orderService) : BaseController
{
    private readonly IOrderService _orderService = orderService;

    [HttpGet]
    public async Task<ActionResult<IEnumerable<OrderDto>>> GetAllOrders()
    {
        var orders = await _orderService.GetAllOrdersAsync();
        return Ok(orders);
    }

    [HttpGet("{orderId}")]
    public async Task<ActionResult<OrderDto>> GetOrder(string orderId)
    {
        var order = await _orderService.GetOrderAsync(orderId);
        if (order is null) return NotFound();
        return Ok(order);
    }
}
