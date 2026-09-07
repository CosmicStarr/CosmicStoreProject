namespace Data.Interfaces
{
    public interface ICacheService
    {
        Task ObjectToCache(string key,object itemToCache,TimeSpan timetolive);
        Task<T?> GetCachedObject<T>(string key);
        Task RemoveData(string key);
    }
}