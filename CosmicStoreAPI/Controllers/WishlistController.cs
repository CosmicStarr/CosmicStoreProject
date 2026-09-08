using System.Security.Claims;
using Data.Interfaces;
using Data.Util;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Models;
using Models.AngularDTOs;

namespace CosmicStoreAPI.Controllers;

[Authorize]
public class WishlistController(IStoreUnitOfWork storeUnitOfWork) : BaseController
{
    private readonly IStoreUnitOfWork _storeUnitOfWork = storeUnitOfWork;

    [HttpGet]
    public async Task<ActionResult<IEnumerable<WishlistItemDto>>> GetWishlist()
    {
        var userId = GetUserId();
        var items = await _storeUnitOfWork.Repository<WishlistItem>()
            .GetAllParams(new PageParams { PageNumber = 1, PageSize = 200 }, w => w.AppUserId == userId, includeProperties: "Product");

        return Ok(items
            .OrderByDescending(w => w.AddedAt)
            .Select(MapToDto));
    }

    [HttpPost("{productId}")]
    public async Task<ActionResult<WishlistItemDto>> AddToWishlist(string productId)
    {
        var userId = GetUserId();
        var productRepo = _storeUnitOfWork.Repository<Products>();
        var product = await productRepo.GetFirstOrDefault(p => p.Id == productId);
        if (product is null) return NotFound(new { message = "Product not found." });

        var wishlistRepo = _storeUnitOfWork.Repository<WishlistItem>();
        var existing = await wishlistRepo.GetFirstOrDefault(w => w.AppUserId == userId && w.ProductId == productId);
        if (existing is not null)
        {
            existing.Product = product;
            return Ok(MapToDto(existing));
        }

        var item = new WishlistItem
        {
            AppUserId = userId,
            ProductId = productId,
            Product = product
        };

        wishlistRepo.Add(item);
        await _storeUnitOfWork.Complete();

        return Ok(MapToDto(item));
    }

    [HttpDelete("{productId}")]
    public async Task<IActionResult> RemoveFromWishlist(string productId)
    {
        var userId = GetUserId();
        var wishlistRepo = _storeUnitOfWork.Repository<WishlistItem>();
        var item = await wishlistRepo.GetFirstOrDefault(w => w.AppUserId == userId && w.ProductId == productId);

        if (item is null) return NotFound();

        wishlistRepo.Remove(item);
        await _storeUnitOfWork.Complete();

        return NoContent();
    }

    [HttpGet("contains/{productId}")]
    public async Task<ActionResult<bool>> IsInWishlist(string productId)
    {
        var userId = GetUserId();
        var item = await _storeUnitOfWork.Repository<WishlistItem>()
            .GetFirstOrDefault(w => w.AppUserId == userId && w.ProductId == productId);

        return Ok(item is not null);
    }

    private string GetUserId()
    {
        return User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? throw new UnauthorizedAccessException();
    }

    private static WishlistItemDto MapToDto(WishlistItem item) => new()
    {
        Id = item.Id,
        ProductId = item.ProductId,
        NameEn = item.Product?.NameEn ?? string.Empty,
        Sku = item.Product?.Sku ?? string.Empty,
        SellPrice = item.Product?.SellPrice ?? 0,
        BigImage = item.Product?.BigImage,
        AddedAt = item.AddedAt
    };
}
