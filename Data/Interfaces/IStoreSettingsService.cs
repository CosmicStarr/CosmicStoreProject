using Models;
using Models.AngularDTOs;

namespace Data.Interfaces;

public interface IStoreSettingsService
{
    Task<decimal> GetDefaultMarkupAsync();
    Task<bool> IsCatalogSyncEnabledAsync();
    Task<int> GetCatalogSyncHoursAsync();
    Task<StoreRuntimeSettingsDto> GetSettingsAsync();
    Task<StoreRuntimeSettingsDto> UpdateSettingsAsync(UpdateStoreRuntimeSettingsRequest request);
    Task MarkSyncCompletedAsync(string cacheKey);
}
