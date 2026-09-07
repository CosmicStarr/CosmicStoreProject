using System.Text.Json;
using Data.Interfaces;
using StackExchange.Redis;

namespace Data.Classes
{
    public class CacheService : ICacheService
    {
        private readonly IDatabase _database;
        public CacheService(IConnectionMultiplexer redis)
        {
            _database = redis.GetDatabase();
        }
        public async Task<T?> GetCachedObject<T>(string key)
        {
            var data = await _database.StringGetAsync(key);
            if(data.IsNullOrEmpty) return default;
            
            // Add options to ignore case when mapping JSON back to C# objects
            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };
            
            return JsonSerializer.Deserialize<T>(data.ToString(), options);
        }

        public async Task ObjectToCache(string key, object itemToCache, TimeSpan timetolive)
        {
            if(itemToCache is null) return;
            var option = new JsonSerializerOptions
            {
               PropertyNamingPolicy = JsonNamingPolicy.CamelCase  
            };
            var serializedObject = JsonSerializer.Serialize(itemToCache,option);
            await _database.StringSetAsync(key,serializedObject,timetolive);
        }

        public async Task RemoveData(string key)
        {
            // Deletes the specific key from Redis
            await _database.KeyDeleteAsync(key);
        }
    }
}