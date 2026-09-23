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
            try
            {
                var data = await _database.StringGetAsync(key);
                if (data.IsNullOrEmpty) return default;

                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                };

                return JsonSerializer.Deserialize<T>(data.ToString(), options);
            }
            catch (RedisException)
            {
                // Cache is best-effort; fall through to SQL when Redis is unreachable.
                return default;
            }
        }

        /// <summary>Serializes an object to camelCase JSON and stores it in Redis with a TTL.</summary>
        public async Task ObjectToCache(string key, object itemToCache, TimeSpan timetolive)
        {
            if (itemToCache is null) return;
            try
            {
                var option = new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                };
                var serializedObject = JsonSerializer.Serialize(itemToCache, option);
                await _database.StringSetAsync(key, serializedObject, timetolive);
            }
            catch (RedisException)
            {
                // Ignore cache write failures.
            }
        }

        /// <summary>Deletes one Redis key (used after publish/create/edit so the catalog refreshes).</summary>
        public async Task RemoveData(string key)
        {
            try
            {
                await _database.KeyDeleteAsync(key);
            }
            catch (RedisException)
            {
                // Ignore cache delete failures.
            }
        }

        /// <summary>Increments a Redis counter and sets TTL on the first write.</summary>
        public async Task<long> IncrementAsync(string key, TimeSpan timeToLive)
        {
            try
            {
                var value = await _database.StringIncrementAsync(key);
                if (value == 1)
                {
                    await _database.KeyExpireAsync(key, timeToLive);
                }

                return value;
            }
            catch (RedisException)
            {
                return 0;
            }
        }
    }
}