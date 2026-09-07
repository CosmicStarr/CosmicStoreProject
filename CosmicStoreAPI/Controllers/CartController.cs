using System.Security.Claims;
using Data.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Models;
using Models.AngularDTOs;

namespace CosmicStoreAPI.Controllers;

public class CartController(IShoppingCartService cartService) : BaseController
{
    private readonly IShoppingCartService _cartService = cartService;

    [HttpGet]
    public async Task<ActionResult<ShoppingCart>> GetCart([FromQuery] string cartId)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var cart = await _cartService.GetShoppingCartAsync(cartId, userId);
        if (cart is null) return NotFound(new { message = "Cart not found." });
        return Ok(cart);
    }

    [HttpPost("items")]
    public async Task<ActionResult<ShoppingCart>> AddItem([FromBody] AddCartItemDto dto)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var cart = await _cartService.AddItemAsync(dto.CartId, dto.ProductId, dto.Quantity, userId);
        if (cart is null) return BadRequest(new { message = "Unable to add item to cart." });
        return Ok(cart);
    }

    [HttpPut]
    public async Task<ActionResult<ShoppingCart>> UpdateCart([FromBody] ShoppingCart cart)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var updated = await _cartService.UpdateShoppingCartAsync(cart, userId);
        if (updated is null) return BadRequest(new { message = "Unable to update cart." });
        return Ok(updated);
    }

    [HttpDelete("items/{sku}")]
    public async Task<ActionResult<ShoppingCart>> RemoveItem(string sku, [FromQuery] string cartId)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var cart = await _cartService.RemoveItemAsync(cartId, sku, userId);
        if (cart is null) return NotFound(new { message = "Cart not found." });
        return Ok(cart);
    }

    [Authorize]
    [HttpPost("merge")]
    public async Task<ActionResult<ShoppingCart>> MergeCart([FromBody] MergeCartDto dto)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var cart = await _cartService.MergeGuestCartAsync(dto.GuestCartId, userId);
        if (cart is null) return BadRequest(new { message = "Unable to merge carts." });
        return Ok(cart);
    }

    [HttpDelete]
    public async Task<ActionResult> ClearCart([FromQuery] string cartId)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var deleted = await _cartService.DeleteCartAsync(cartId, userId);
        if (!deleted) return NotFound(new { message = "Cart not found." });
        return NoContent();
    }
}
