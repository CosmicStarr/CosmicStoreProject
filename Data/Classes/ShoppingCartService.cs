using System.Text.Json;
using System.Text.Json.Serialization;
using Data.Interfaces;
using Models;
using StackExchange.Redis;

namespace Data.Classes;

/// <summary>
/// Redis shopping cart. Lines merge by SKU; signed-in users are linked via ShoppingCartSessionId.
/// </summary>
public class ShoppingCartService : IShoppingCartService
{
    private readonly IDatabase _database;
    private readonly IStoreUnitOfWork _storeUnitOfWork;
    private static readonly TimeSpan CartExpiry = TimeSpan.FromDays(30);
    private static readonly JsonSerializerOptions CartJson = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        ReferenceHandler = ReferenceHandler.IgnoreCycles,
    };

    public ShoppingCartService(IDatabase database, IStoreUnitOfWork storeUnitOfWork)
    {
        _database = database;
        _storeUnitOfWork = storeUnitOfWork;
    }

    /// <summary>Loads the guest or user cart from Redis. Signed-in requests with a guest cart id merge first.</summary>
    public async Task<ShoppingCart?> GetShoppingCartAsync(string? id, string? userId)
    {
        var requestedId = NormalizeId(id);

        if (!string.IsNullOrEmpty(userId) && requestedId is not null)
        {
            var requested = await ReadCartAsync(requestedId);
            if (requested?.ShoppingCartItems.Count > 0)
            {
                return await MergeGuestCartAsync(requestedId, userId);
            }
        }

        var storageId = await ResolveStorageIdAsync(requestedId, userId);
        if (storageId is null)
        {
            return new ShoppingCart { Id = requestedId ?? string.Empty };
        }

        return await ReadCartAsync(storageId) ?? new ShoppingCart { Id = storageId };
    }

    /// <summary>Writes the full cart JSON to Redis (30-day TTL) and links it to the user when signed in.</summary>
    public async Task<ShoppingCart?> UpdateShoppingCartAsync(ShoppingCart shoppingCart, string? userId)
    {
        var saved = await _database.StringSetAsync(
            shoppingCart.Id,
            JsonSerializer.Serialize(shoppingCart, CartJson),
            CartExpiry);

        if (!saved) return null;

        if (!string.IsNullOrEmpty(userId))
            await LinkCartToUserAsync(shoppingCart.Id, userId);

        return shoppingCart;
    }

    /// <summary>
    /// Adds a storefront product line. Quantity merges onto an existing line with the same resolved SKU.
    /// </summary>
    public async Task<ShoppingCart?> AddItemAsync(string? cartId, string productId, int quantity, string? userId, string? sku = null)
    {
        if (quantity < 1) return null;

        var product = await _storeUnitOfWork.Repository<Products>()
            .GetFirstOrDefault(p => p.Id == productId);

        if (product is null) return null;

        var lineSku = await ResolveLineSkuAsync(product, sku);
        if (lineSku is null) return null;

        product.ProductImages = null;

        var storageId = await ResolveStorageIdAsync(cartId, userId)
            ?? NormalizeId(cartId)
            ?? Guid.NewGuid().ToString();

        var cart = await ReadCartAsync(storageId) ?? new ShoppingCart { Id = storageId };

        var existing = cart.ShoppingCartItems.FirstOrDefault(i =>
            string.Equals(i.Sku, lineSku, StringComparison.OrdinalIgnoreCase));
        if (existing is not null)
        {
            existing.Amount += quantity;
            existing.price = product.SellPrice;
            existing.Name = product.NameEn;
        }
        else
        {
            cart.ShoppingCartItems.Add(new CartItems
            {
                Name = product.NameEn,
                Sku = lineSku,
                price = product.SellPrice,
                Amount = quantity
            });
        }

        return await UpdateShoppingCartAsync(cart, userId);
    }

    /// <summary>Removes one cart line by SKU.</summary>
    public async Task<ShoppingCart?> RemoveItemAsync(string cartId, string sku, string? userId)
    {
        var storageId = await ResolveStorageIdAsync(cartId, userId) ?? NormalizeId(cartId);
        if (storageId is null) return null;

        var cart = await ReadCartAsync(storageId) ?? new ShoppingCart { Id = storageId };
        cart.ShoppingCartItems.RemoveAll(i => i.Sku == sku);
        return await UpdateShoppingCartAsync(cart, userId);
    }

    /// <summary>Merges a guest Redis cart into the signed-in user's cart after login (same SKUs add quantities).</summary>
    public async Task<ShoppingCart?> MergeGuestCartAsync(string guestCartId, string userId)
    {
        var guestId = NormalizeId(guestCartId);
        var userSession = await _storeUnitOfWork.Repository<ShoppingCartSessionId>()
            .GetFirstOrDefault(s => s.ApplicationUser == userId);
        var userCartId = NormalizeId(userSession?.ActualShoppingCartId);

        if (guestId is not null && userCartId is not null
            && string.Equals(guestId, userCartId, StringComparison.OrdinalIgnoreCase))
        {
            return await ReadCartAsync(guestId) ?? new ShoppingCart { Id = guestId };
        }

        var guestCart = guestId is null ? null : await ReadCartAsync(guestId);
        var userCart = userCartId is null ? null : await ReadCartAsync(userCartId);

        if (guestCart is null || guestCart.ShoppingCartItems.Count == 0)
        {
            if (userCart is not null)
            {
                return userCart;
            }

            var fallbackId = userCartId ?? guestId ?? Guid.NewGuid().ToString();
            await LinkCartToUserAsync(fallbackId, userId);
            return new ShoppingCart { Id = fallbackId };
        }

        if (userCart is null)
        {
            await LinkCartToUserAsync(guestCart.Id, userId);
            return guestCart;
        }

        foreach (var item in guestCart.ShoppingCartItems)
        {
            var existing = userCart.ShoppingCartItems.FirstOrDefault(i =>
                string.Equals(i.Sku, item.Sku, StringComparison.OrdinalIgnoreCase));
            if (existing is not null)
            {
                existing.Amount += item.Amount;
            }
            else
            {
                userCart.ShoppingCartItems.Add(item);
            }
        }

        if (guestId is not null
            && !string.Equals(guestId, userCart.Id, StringComparison.OrdinalIgnoreCase))
        {
            await _database.KeyDeleteAsync(guestId);
        }

        return await UpdateShoppingCartAsync(userCart, userId) ?? userCart;
    }

    /// <summary>Deletes the Redis cart and the SQL session row that points at it.</summary>
    public async Task<bool> DeleteCartAsync(string id, string? userId)
    {
        var cartId = await ResolveStorageIdAsync(id, userId) ?? NormalizeId(id) ?? id;
        var session = await _storeUnitOfWork.Repository<ShoppingCartSessionId>()
            .GetFirstOrDefault(s => s.ActualShoppingCartId == cartId);

        if (session is not null)
        {
            _storeUnitOfWork.Repository<ShoppingCartSessionId>().Remove(session);
            await _storeUnitOfWork.Complete();
        }

        return await _database.KeyDeleteAsync(cartId);
    }

    /// <summary>
    /// Accepts the parent SKU, a gallery skuPhoto, or a ProductVariant SKU. Returns null if the SKU is not on this product.
    /// </summary>
    private async Task<string?> ResolveLineSkuAsync(Products product, string? requestedSku)
    {
        var requested = requestedSku?.Trim();
        if (string.IsNullOrWhiteSpace(requested) ||
            string.Equals(requested, product.Sku, StringComparison.OrdinalIgnoreCase))
        {
            return product.Sku;
        }

        var image = await _storeUnitOfWork.Repository<ProductImage>()
            .GetFirstOrDefault(img => img.ProductId == product.Id && img.SkuPhoto == requested);
        if (image is not null && !string.IsNullOrWhiteSpace(image.SkuPhoto))
        {
            return image.SkuPhoto;
        }

        var variant = await _storeUnitOfWork.Repository<Models.ProductVariant>()
            .GetFirstOrDefault(v => v.ProductId == product.Id && v.Sku == requested);
        if (variant is not null && !string.IsNullOrWhiteSpace(variant.Sku))
        {
            return variant.Sku;
        }

        return null;
    }

    /// <summary>Loads the cart linked to a signed-in user via ShoppingCartSessionId.</summary>
    private async Task<ShoppingCart?> GetUserCartAsync(string userId)
    {
        var session = await _storeUnitOfWork.Repository<ShoppingCartSessionId>()
            .GetFirstOrDefault(s => s.ApplicationUser == userId);

        if (session is null) return null;
        return await ReadCartAsync(session.ActualShoppingCartId)
            ?? new ShoppingCart { Id = session.ActualShoppingCartId };
    }

    /// <summary>Prefers the user's linked cart id; otherwise uses the guest cart id if it looks valid.</summary>
    private async Task<string?> ResolveStorageIdAsync(string? id, string? userId)
    {
        if (!string.IsNullOrEmpty(userId))
        {
            var session = await _storeUnitOfWork.Repository<ShoppingCartSessionId>()
                .GetFirstOrDefault(s => s.ApplicationUser == userId);

            if (session is not null && !string.IsNullOrWhiteSpace(session.ActualShoppingCartId))
                return session.ActualShoppingCartId;
        }

        return NormalizeId(id);
    }

    /// <summary>Creates or updates the SQL row that maps a user to a Redis cart id.</summary>
    private async Task LinkCartToUserAsync(string cartId, string userId)
    {
        var existing = await _storeUnitOfWork.Repository<ShoppingCartSessionId>()
            .GetFirstOrDefault(s => s.ApplicationUser == userId);

        if (existing is not null)
        {
            existing.ActualShoppingCartId = cartId;
            // Lookups are AsNoTracking, so Complete() will not persist this unless we attach it.
            _storeUnitOfWork.Repository<ShoppingCartSessionId>().Update(existing);
        }
        else
        {
            _storeUnitOfWork.Repository<ShoppingCartSessionId>().Add(new ShoppingCartSessionId
            {
                ActualShoppingCartId = cartId,
                ApplicationUser = userId
            });
        }

        await _storeUnitOfWork.Complete();
    }

    /// <summary>Deserializes a cart from Redis, or null if the key is missing or corrupt.</summary>
    private async Task<ShoppingCart?> ReadCartAsync(string cartId)
    {
        var data = await _database.StringGetAsync(cartId);
        if (data.IsNullOrEmpty) return null;

        try
        {
            return JsonSerializer.Deserialize<ShoppingCart>(data.ToString(), CartJson);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>Rejects empty, "undefined", and "null" client cart ids.</summary>
    private static string? NormalizeId(string? id)
    {
        if (string.IsNullOrWhiteSpace(id) || id is "undefined" or "null")
            return null;

        return id;
    }
}
