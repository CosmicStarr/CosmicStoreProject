using Models;

namespace Data.Interfaces;

public interface IShoppingCartService
{
    Task<ShoppingCart?> GetShoppingCartAsync(string? id, string? userId);
    Task<ShoppingCart?> UpdateShoppingCartAsync(ShoppingCart shoppingCart, string? userId);
    Task<ShoppingCart?> AddItemAsync(string? cartId, string productId, int quantity, string? userId, string? sku = null, int? wishlistId = null);
    Task<ShoppingCart?> RemoveItemAsync(string cartId, string sku, string? userId);
    Task<ShoppingCart?> MergeGuestCartAsync(string guestCartId, string userId);
    Task<bool> DeleteCartAsync(string id, string? userId);
}
