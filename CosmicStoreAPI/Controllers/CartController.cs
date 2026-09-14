using System.Security.Claims;
using Data.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Models;
using Models.AngularDTOs;

namespace CosmicStoreAPI.Controllers;

/// <summary>
/// Redis shopping cart: read, add/remove lines (by SKU), merge guest carts after login, and clear.
/// </summary>
public class CartController(IShoppingCartService cartService) : BaseController
{
    private readonly IShoppingCartService _cartService = cartService;

    /// <summary>
    /// Returns the current cart for the guest cart id or the signed-in user's session.
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<ShoppingCart>> GetCart([FromQuery] string? cartId)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var cart = await _cartService.GetShoppingCartAsync(cartId, userId);
        return Ok(cart);
    }

    /// <summary>
    /// Adds a storefront product line. Lines merge by SKU (selected gallery skuPhoto), not product id.
    /// </summary>
    [HttpPost("items")]
    public async Task<ActionResult<ShoppingCart>> AddItem([FromBody] AddCartItemDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.ProductId) || dto.Quantity < 1)
        {
            return BadRequest(new { message = "A product and quantity are required." });
        }

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var cart = await _cartService.AddItemAsync(dto.CartId, dto.ProductId, dto.Quantity, userId, dto.Sku);
        if (cart is null)
        {
            return BadRequest(new { message = "That product or SKU is not on the storefront, or the cart could not be saved." });
        }

        return Ok(cart);
    }

    /// <summary>
    /// Replaces the saved cart (quantities, shipping choice, Stripe payment ids).
    /// </summary>
    [HttpPut]
    public async Task<ActionResult<ShoppingCart>> UpdateCart([FromBody] ShoppingCart cart)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var updated = await _cartService.UpdateShoppingCartAsync(cart, userId);
        if (updated is null) return BadRequest(new { message = "Unable to update cart." });
        return Ok(updated);
    }

    /// <summary>
    /// Removes one cart line identified by SKU.
    /// </summary>
    [HttpDelete("items/{sku}")]
    public async Task<ActionResult<ShoppingCart>> RemoveItem(string sku, [FromQuery] string cartId)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var cart = await _cartService.RemoveItemAsync(cartId, sku, userId);
        if (cart is null) return Ok(new ShoppingCart { Id = cartId });
        return Ok(cart);
    }

    /// <summary>
    /// Copies a guest Redis cart onto the signed-in user's cart after login.
    /// </summary>
    [Authorize]
    [HttpPost("merge")]
    public async Task<ActionResult<ShoppingCart>> MergeCart([FromBody] MergeCartDto dto)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId))
        {
            return Unauthorized(new { message = "Sign in to merge your cart." });
        }

        var cart = await _cartService.MergeGuestCartAsync(dto.GuestCartId, userId)
            ?? new ShoppingCart { Id = dto.GuestCartId };
        return Ok(cart);
    }

    /// <summary>
    /// Deletes the cart from Redis (and the user session link when signed in).
    /// </summary>
    [HttpDelete]
    public async Task<ActionResult> ClearCart([FromQuery] string cartId)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var deleted = await _cartService.DeleteCartAsync(cartId, userId);
        if (!deleted) return NotFound(new { message = "Cart not found." });
        return NoContent();
    }
}
