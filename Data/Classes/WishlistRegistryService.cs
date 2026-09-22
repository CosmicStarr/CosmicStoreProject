using Data.Interfaces;
using Data.Util;
using Microsoft.Extensions.Configuration;
using Models;
using Stripe;

namespace Data.Classes;

/// <summary>
/// Gift-registry lists: one per user, destination hidden from buyers, address resolved only on the server.
/// </summary>
public class WishlistRegistryService(
    IStoreUnitOfWork storeUnitOfWork,
    IConfiguration configuration) : IWishlistRegistryService
{
    private readonly IStoreUnitOfWork _storeUnitOfWork = storeUnitOfWork;
    private readonly IConfiguration _configuration = configuration;

    public async Task<Wishlist> EnsureUserWishlistAsync(string userId)
    {
        var existing = await _storeUnitOfWork.Repository<Wishlist>()
            .GetFirstOrDefault(list => list.AppUserId == userId);
        if (existing is not null)
        {
            return existing;
        }

        var list = new Wishlist
        {
            AppUserId = userId,
            PublicId = Guid.NewGuid().ToString("N"),
            Name = "My Wishlist",
            CreatedAt = DateTime.UtcNow
        };

        _storeUnitOfWork.Repository<Wishlist>().Add(list);
        await _storeUnitOfWork.Complete();
        return list;
    }

    public async Task<Wishlist?> GetByIdAsync(int wishlistId, bool includeAddress = true, bool includeItems = false)
    {
        var includes = string.Join(",", new[]
        {
            includeAddress ? "ShippingAddress" : null,
            includeItems ? "Items.Product" : null
        }.Where(value => !string.IsNullOrWhiteSpace(value)));

        return await _storeUnitOfWork.Repository<Wishlist>()
            .GetFirstOrDefault(
                list => list.Id == wishlistId,
                includeProperties: string.IsNullOrWhiteSpace(includes) ? null : includes);
    }

    public async Task<Wishlist?> GetPublicRegistryAsync(string publicId)
    {
        if (string.IsNullOrWhiteSpace(publicId))
        {
            return null;
        }

        var list = await _storeUnitOfWork.Repository<Wishlist>()
            .GetFirstOrDefault(
                w => w.PublicId == publicId.Trim(),
                includeProperties: "ShippingAddress,Items.Product");

        if (list is null || !list.IsGiftRegistry || list.ShippingAddress is null)
        {
            return null;
        }

        return list;
    }

    public async Task<Wishlist> RequirePublicRegistryAsync(int wishlistId)
    {
        var list = await GetByIdAsync(wishlistId, includeAddress: true);
        if (list is null || !list.IsGiftRegistry || list.ShippingAddress is null)
        {
            throw new InvalidOperationException("That gift registry is not available.");
        }

        return list;
    }

    public string BuildShareUrl(string publicId)
    {
        var baseUrl = StoreUrls.Absolute(_configuration, "ReturnPath:publicWishlist", "/wishlist");
        return $"{baseUrl}/{publicId}";
    }

    public ChargeShippingOptions ToStripeShipping(UserAddress address)
    {
        return new ChargeShippingOptions
        {
            Name = address.FullName,
            Address = new AddressOptions
            {
                Line1 = address.StreetAddress,
                City = address.City,
                State = address.ProvinceOrState,
                PostalCode = address.ZipCode,
                Country = string.IsNullOrWhiteSpace(address.CountryCode)
                    ? "US"
                    : address.CountryCode.Trim().ToUpperInvariant()
            }
        };
    }

    /// <summary>
    /// Returns the single registry id on the basket, or null when nothing is bound.
    /// Throws when registry gifts are mixed with other destinations.
    /// </summary>
    public static int? BoundWishlistId(IEnumerable<CartItems> items)
    {
        var ids = items.Select(item => item.WishlistId).Distinct().ToList();
        if (ids.Count == 0)
        {
            return null;
        }

        if (ids.Count > 1)
        {
            throw new InvalidOperationException(
                "Your cart mixes a gift registry with other items. Check out registry gifts separately from other products.");
        }

        return ids[0];
    }
}
