using System.Text.Json;
using Data.Interfaces;
using Models;
using StackExchange.Redis;

namespace Data.Classes
{
    public class ShoppingCartService : IShoppingCartService
    {
        private readonly IDatabase _database;
        private readonly IUnitOfWork _unitOfWork;
        public ShoppingCartService(IDatabase database,IUnitOfWork unitOfWork)
        {
            _database = database;
            _unitOfWork = unitOfWork;
            
        }
        public async Task<bool> DeleteCartAsync(string Id, string? user)
        {
            var savedCart = await _unitOfWork.Repository<ShoppingCartSessionId>().GetFirstOrDefault(x=>x.ActualShoppingCartId==Id && x.ApplicationUser == user);
            if(savedCart is not null)
            {
                _unitOfWork.Repository<ShoppingCartSessionId>().Remove(savedCart);
            }
            await _unitOfWork.Complete();
            return await _database.KeyDeleteAsync(Id);
        }
      
        public async Task<ShoppingCart> GetShoppingCartAsync(string Id, string? user)
        {
        #nullable disable
            if(Id == "undefined" || Id == "null"||string.IsNullOrEmpty(Id)) return null;
            var Data = await _database.StringGetAsync(Id);
            if(Data.IsNullOrEmpty) return null;
            return System.Text.Json.JsonSerializer.Deserialize<ShoppingCart>(Data.ToString());
        }
        
        public async Task<ShoppingCart> UpdateShoppingCartAsync(ShoppingCart shoppingCart, string user)
        {
            var savedCart = await _unitOfWork.Repository<ShoppingCartSessionId>().GetFirstOrDefault(x=>x.ApplicationUser == user);
            if(savedCart is null && user is not null)
            {
                var cartInfo = new ShoppingCartSessionId
                {
                  ActualShoppingCartId = shoppingCart.Id,
                  ApplicationUser = user 
                };
                _unitOfWork.Repository<ShoppingCartSessionId>().Add(cartInfo);
                await _unitOfWork.Complete();
                var nuCart = await _database.StringSetAsync(shoppingCart.Id,JsonSerializer.Serialize(shoppingCart));
                if(!nuCart) return null;
                return await GetShoppingCartAsync(savedCart.ActualShoppingCartId,user);
            }
            else if(savedCart is not null)
            {
                var UpdatedCart = await _database.StringSetAsync(savedCart.ActualShoppingCartId,JsonSerializer.Serialize(shoppingCart));
                if(!UpdatedCart) return null;
                return await GetShoppingCartAsync(savedCart.ActualShoppingCartId,user);
            }
            else
            {
                var guestCart = await _database.StringSetAsync(shoppingCart.Id,JsonSerializer.Serialize(shoppingCart));
                if(!guestCart) return null;
                return await GetShoppingCartAsync(shoppingCart.Id,user);
            }
        }
    }
}