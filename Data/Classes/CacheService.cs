using System.Text.Json;
using Data.Interfaces;
using StackExchange.Redis;

namespace Data.Classes
{
    /// <summary>
    /// Redis JSON cache used for the storefront product list and related keys.
    /// </summary>
    public class CacheService : ICacheService
    {
        private readonly IDatabase _database;
        public CacheService(IConnectionMultiplexer redis)
        {
            _database = redis.GetDatabase();
        }

        /// <summary>Reads and deserializes a cached object, or returns default if the key is missing.</summary>
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

        /// <summary>Serializes an object to camelCase JSON and stores it in Redis with a TTL.</summary>
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

        /// <summary>Deletes one Redis key (used after publish/create/edit so the catalog refreshes).</summary>
        public async Task RemoveData(string key)
        {
            await _database.KeyDeleteAsync(key);
        }

        /// <summary>Increments a Redis counter and sets TTL on the first write.</summary>
        public async Task<long> IncrementAsync(string key, TimeSpan timeToLive)
        {
            var value = await _database.StringIncrementAsync(key);
            if (value == 1)
            {
                await _database.KeyExpireAsync(key, timeToLive);
            }

            return value;
        }
    }
}