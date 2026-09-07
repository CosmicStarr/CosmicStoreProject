using Models;

namespace Data.Interfaces
{
    public interface IShoppingCartService
    {
        Task<ShoppingCart> GetShoppingCartAsync(string Id,string?user);
        Task<ShoppingCart> UpdateShoppingCartAsync(ShoppingCart shoppingCart,string?user);
        Task<bool> DeleteCartAsync(string Id,string? user);
    }
}