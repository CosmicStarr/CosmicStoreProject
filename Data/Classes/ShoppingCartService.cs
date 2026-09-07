using System.Text.Json;
using Data.Interfaces;
using Microsoft.EntityFrameworkCore;
using Models;
using StackExchange.Redis;

namespace Data.Classes;

public class ShoppingCartService : IShoppingCartService
{
    private readonly IDatabase _database;
    private readonly IStoreUnitOfWork _storeUnitOfWork;
    private static readonly TimeSpan CartExpiry = TimeSpan.FromDays(30);

    public ShoppingCartService(IDatabase database, IStoreUnitOfWork storeUnitOfWork)
    {
        _database = database;
        _storeUnitOfWork = storeUnitOfWork;
    }

    public async Task<ShoppingCart?> GetShoppingCartAsync(string id, string? userId)
    {
        if (string.IsNullOrWhiteSpace(id) || id is "undefined" or "null")
            return null;

        var cartId = await ResolveCartIdAsync(id, userId);
        if (cartId is null) return null;

        var data = await _database.StringGetAsync(cartId);
        if (data.IsNullOrEmpty) return null;

        return JsonSerializer.Deserialize<ShoppingCart>(data.ToString());
    }

    public async Task<ShoppingCart?> UpdateShoppingCartAsync(ShoppingCart shoppingCart, string? userId)
    {
        var saved = await _database.StringSetAsync(
            shoppingCart.Id,
            JsonSerializer.Serialize(shoppingCart),
            CartExpiry);

        if (!saved) return null;

        if (!string.IsNullOrEmpty(userId))
            await LinkCartToUserAsync(shoppingCart.Id, userId);

        return await GetShoppingCartAsync(shoppingCart.Id, userId);
    }

    public async Task<ShoppingCart?> AddItemAsync(string? cartId, string productId, int quantity, string? userId)
    {
        if (quantity < 1) return null;

        var product = await _storeUnitOfWork.Repository<Products>()
            .GetFirstOrDefault(p => p.Id == productId);

        if (product is null) return null;

        cartId ??= Guid.NewGuid().ToString();
        var cart = await GetShoppingCartAsync(cartId, userId) ?? new ShoppingCart { Id = cartId };

        var existing = cart.ShoppingCartItems.FirstOrDefault(i => i.Sku == product.Sku);
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
                Sku = product.Sku,
                price = product.SellPrice,
                Amount = quantity
            });
        }

        return await UpdateShoppingCartAsync(cart, userId);
    }

    public async Task<ShoppingCart?> RemoveItemAsync(string cartId, string sku, string? userId)
    {
        var cart = await GetShoppingCartAsync(cartId, userId);
        if (cart is null) return null;

        cart.ShoppingCartItems.RemoveAll(i => i.Sku == sku);
        return await UpdateShoppingCartAsync(cart, userId);
    }

    public async Task<ShoppingCart?> MergeGuestCartAsync(string guestCartId, string userId)
    {
        var guestCart = await GetShoppingCartAsync(guestCartId, null);
        if (guestCart is null || guestCart.ShoppingCartItems.Count == 0)
            return await GetUserCartAsync(userId);

        var userSession = await _storeUnitOfWork.Repository<ShoppingCartSessionId>()
            .GetFirstOrDefault(s => s.ApplicationUser == userId);

        if (userSession is null)
        {
            await LinkCartToUserAsync(guestCartId, userId);
            return guestCart;
        }

        var userCart = await GetShoppingCartAsync(userSession.ActualShoppingCartId, userId);
        if (userCart is null)
        {
            await LinkCartToUserAsync(guestCartId, userId);
            return guestCart;
        }

        foreach (var item in guestCart.ShoppingCartItems)
        {
            var existing = userCart.ShoppingCartItems.FirstOrDefault(i => i.Sku == item.Sku);
            if (existing is not null)
                existing.Amount += item.Amount;
            else
                userCart.ShoppingCartItems.Add(item);
        }

        await _database.KeyDeleteAsync(guestCartId);
        return await UpdateShoppingCartAsync(userCart, userId);
    }

    public async Task<bool> DeleteCartAsync(string id, string? userId)
    {
        var cartId = await ResolveCartIdAsync(id, userId) ?? id;
        var session = await _storeUnitOfWork.Repository<ShoppingCartSessionId>()
            .GetFirstOrDefault(s => s.ActualShoppingCartId == cartId);

        if (session is not null)
        {
            _storeUnitOfWork.Repository<ShoppingCartSessionId>().Remove(session);
            await _storeUnitOfWork.Complete();
        }

        return await _database.KeyDeleteAsync(cartId);
    }

    private async Task<ShoppingCart?> GetUserCartAsync(string userId)
    {
        var session = await _storeUnitOfWork.Repository<ShoppingCartSessionId>()
            .GetFirstOrDefault(s => s.ApplicationUser == userId);

        return session is null
            ? null
            : await GetShoppingCartAsync(session.ActualShoppingCartId, userId);
    }

    private async Task<string?> ResolveCartIdAsync(string id, string? userId)
    {
        if (!string.IsNullOrEmpty(userId))
        {
            var session = await _storeUnitOfWork.Repository<ShoppingCartSessionId>()
                .GetFirstOrDefault(s => s.ApplicationUser == userId);

            if (session is not null)
                return session.ActualShoppingCartId;
        }

        return id;
    }

    private async Task LinkCartToUserAsync(string cartId, string userId)
    {
        var existing = await _storeUnitOfWork.Repository<ShoppingCartSessionId>()
            .GetFirstOrDefault(s => s.ApplicationUser == userId);

        if (existing is not null)
        {
            if (existing.ActualShoppingCartId != cartId)
                existing.ActualShoppingCartId = cartId;
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
}
