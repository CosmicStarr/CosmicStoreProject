using System.Security.Claims;
using Data.Interfaces;
using Data.Util;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Models;
using Models.AngularDTOs;

namespace CosmicStoreAPI.Controllers;

/// <summary>
/// Saved-product lists. Public gift registries never return street, city, or ZIP.
/// </summary>
[Authorize]
public class WishlistController(
    IStoreUnitOfWork storeUnitOfWork,
    IWishlistRegistryService registryService) : BaseController
{
    private readonly IStoreUnitOfWork _storeUnitOfWork = storeUnitOfWork;
    private readonly IWishlistRegistryService _registryService = registryService;

    /// <summary>The signed-in user's list, including registry settings. Address fields are never included.</summary>
    [HttpGet]
    public async Task<ActionResult<WishlistDto>> GetWishlist()
    {
        var userId = GetUserId();
        await _registryService.EnsureUserWishlistAsync(userId);
        var list = await LoadOwnerWishlistAsync(userId);
        return list is null ? NotFound() : Ok(MapOwner(list));
    }

    /// <summary>Public gift registry. Returns only a masked shipping label — never a physical address.</summary>
    [AllowAnonymous]
    [HttpGet("public/{publicId}")]
    public async Task<ActionResult<PublicWishlistDto>> GetPublicWishlist(string publicId)
    {
        var list = await _registryService.GetPublicRegistryAsync(publicId);
        if (list is null)
        {
            return NotFound(new { message = "That gift registry was not found." });
        }

        return Ok(MapPublic(list));
    }

    /// <summary>Turns the list into a shareable gift registry, or updates its name and destination.</summary>
    [HttpPut]
    public async Task<ActionResult<WishlistDto>> UpdateWishlist(UpdateWishlistRequest request)
    {
        var userId = GetUserId();
        var list = await _storeUnitOfWork.Repository<Wishlist>()
            .GetFirstOrDefault(w => w.AppUserId == userId);
        list ??= await _registryService.EnsureUserWishlistAsync(userId);

        if (!string.IsNullOrWhiteSpace(request.Name))
        {
            list.Name = request.Name.Trim();
            if (list.Name.Length > 80)
            {
                list.Name = list.Name[..80];
            }
        }

        if (request.ShippingAddressId is int addressId)
        {
            var address = await _storeUnitOfWork.Repository<UserAddress>()
                .GetFirstOrDefault(a => a.Id == addressId && a.AppUserId == userId);
            if (address is null)
            {
                return BadRequest(new { message = "Choose one of your saved addresses." });
            }

            list.ShippingAddressId = address.Id;
        }

        if (request.IsGiftRegistry)
        {
            if (list.ShippingAddressId is null)
            {
                return BadRequest(new { message = "Save a delivery address before sharing this list as a gift registry." });
            }

            list.IsGiftRegistry = true;
        }
        else
        {
            list.IsGiftRegistry = false;
        }

        _storeUnitOfWork.Repository<Wishlist>().Update(list);
        await _storeUnitOfWork.Complete();

        var fresh = await LoadOwnerWishlistAsync(userId);
        return Ok(MapOwner(fresh!));
    }

    /// <summary>
    /// Adds a published product to the wishlist, or returns the existing row if it is already saved.
    /// </summary>
    [HttpPost("{productId}")]
    public async Task<ActionResult<WishlistItemDto>> AddToWishlist(string productId)
    {
        var userId = GetUserId();
        var productRepo = _storeUnitOfWork.Repository<Products>();
        var product = await productRepo.GetFirstOrDefault(p => p.Id == productId);
        if (product is null) return NotFound(new { message = "Product not found." });

        var list = await _registryService.EnsureUserWishlistAsync(userId);
        var wishlistRepo = _storeUnitOfWork.Repository<WishlistItem>();
        var existing = await wishlistRepo.GetFirstOrDefault(w => w.AppUserId == userId && w.ProductId == productId);
        if (existing is not null)
        {
            existing.Product = product;
            return Ok(MapItem(existing));
        }

        var item = new WishlistItem
        {
            WishlistId = list.Id,
            AppUserId = userId,
            ProductId = productId
        };

        wishlistRepo.Add(item);
        await _storeUnitOfWork.Complete();

        item.Product = product;
        return Ok(MapItem(item));
    }

    /// <summary>
    /// Removes a product from the current user's wishlist.
    /// </summary>
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

    /// <summary>
    /// Returns whether the given product is already on the current user's wishlist.
    /// </summary>
    [HttpGet("contains/{productId}")]
    public async Task<ActionResult<bool>> IsInWishlist(string productId)
    {
        var userId = GetUserId();
        var item = await _storeUnitOfWork.Repository<WishlistItem>()
            .GetFirstOrDefault(w => w.AppUserId == userId && w.ProductId == productId);

        return Ok(item is not null);
    }

    private async Task<Wishlist?> LoadOwnerWishlistAsync(string userId)
    {
        return await _storeUnitOfWork.Repository<Wishlist>()
            .GetFirstOrDefault(
                w => w.AppUserId == userId,
                includeProperties: "ShippingAddress,Items.Product");
    }

    private WishlistDto MapOwner(Wishlist list)
    {
        var masked = list.IsGiftRegistry && list.ShippingAddress is not null
            ? WishlistPrivacy.MaskedShippingLabel(list.ShippingAddress.FullName)
            : null;

        return new WishlistDto
        {
            Id = list.Id,
            PublicId = list.PublicId,
            Name = list.Name,
            ShippingAddressId = list.ShippingAddressId,
            IsGiftRegistry = list.IsGiftRegistry,
            IsAddressPrivate = list.IsGiftRegistry,
            MaskedShippingLabel = masked,
            ShareUrl = list.IsGiftRegistry ? _registryService.BuildShareUrl(list.PublicId) : null,
            FulfillmentDisclaimer = WishlistPrivacy.FulfillmentDisclaimer,
            Items = (list.Items ?? [])
                .OrderByDescending(item => item.AddedAt)
                .Select(MapItem)
                .ToList()
        };
    }

    private static PublicWishlistDto MapPublic(Wishlist list) => new()
    {
        Id = list.Id,
        PublicId = list.PublicId,
        Name = list.Name,
        MaskedShippingLabel = WishlistPrivacy.MaskedShippingLabel(list.ShippingAddress?.FullName),
        FulfillmentDisclaimer = WishlistPrivacy.FulfillmentDisclaimer,
        Items = (list.Items ?? [])
            .OrderByDescending(item => item.AddedAt)
            .Select(MapItem)
            .ToList()
    };

    private string GetUserId()
    {
        return User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? throw new UnauthorizedAccessException();
    }

    private static WishlistItemDto MapItem(WishlistItem item) => new()
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
