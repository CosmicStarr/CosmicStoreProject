using Models;
using Stripe;

namespace Data.Interfaces;

public interface IWishlistRegistryService
{
    Task<Wishlist> EnsureUserWishlistAsync(string userId);

    Task<Wishlist?> GetByIdAsync(int wishlistId, bool includeAddress = true, bool includeItems = false);

    Task<Wishlist?> GetPublicRegistryAsync(string publicId);

    /// <summary>Loads a shareable gift registry with a saved destination, or throws.</summary>
    Task<Wishlist> RequirePublicRegistryAsync(int wishlistId);

    string BuildShareUrl(string publicId);

    ChargeShippingOptions ToStripeShipping(UserAddress address);
}
