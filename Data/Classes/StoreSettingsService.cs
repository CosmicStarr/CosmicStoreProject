using Data.Interfaces;
using Data.Util;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Configuration;
using Models;
using Models.AngularDTOs;

namespace Data.Classes;

public class StoreSettingsService(
    ApplicationDbStoreContext db,
    IConfiguration configuration,
    IDistributedCache cache) : IStoreSettingsService
{
    private readonly ApplicationDbStoreContext _db = db;
    private readonly IConfiguration _configuration = configuration;
    private readonly IDistributedCache _cache = cache;

    public async Task<decimal> GetDefaultMarkupAsync()
    {
        var settings = await EnsureRowAsync();
        return settings.DefaultMarkup;
    }

    public async Task<bool> IsCatalogSyncEnabledAsync()
    {
        var settings = await EnsureRowAsync();
        return settings.CatalogSyncEnabled;
    }

    public async Task<int> GetCatalogSyncHoursAsync()
    {
        var settings = await EnsureRowAsync();
        return NormalizeCatalogHours(settings.CatalogSyncHours);
    }

    public async Task<StoreRuntimeSettingsDto> GetSettingsAsync()
    {
        var settings = await EnsureRowAsync();
        return new StoreRuntimeSettingsDto
        {
            DefaultMarkup = settings.DefaultMarkup,
            CatalogSyncEnabled = settings.CatalogSyncEnabled,
            CatalogSyncHours = NormalizeCatalogHours(settings.CatalogSyncHours),
            CatalogLastSyncAt = await ReadTimestampAsync(CjSyncCacheKeys.CatalogLastSync),
            VariantsLastSyncAt = await ReadTimestampAsync(CjSyncCacheKeys.VariantsLastSync),
            StockLastSyncAt = await ReadTimestampAsync(CjSyncCacheKeys.StockLastSync),
            OrdersLastSyncAt = await ReadTimestampAsync(CjSyncCacheKeys.OrdersLastSync),
        };
    }

    public async Task<StoreRuntimeSettingsDto> UpdateSettingsAsync(UpdateStoreRuntimeSettingsRequest request)
    {
        if (request.DefaultMarkup <= 0)
        {
            throw new InvalidOperationException("Default markup must be greater than zero.");
        }

        if (request.CatalogSyncHours is not (6 or 12 or 24))
        {
            throw new InvalidOperationException("Catalog sync hours must be 6, 12, or 24.");
        }

        var settings = await EnsureRowAsync();
        settings.DefaultMarkup = Math.Round(request.DefaultMarkup, 2);
        settings.CatalogSyncEnabled = request.CatalogSyncEnabled;
        settings.CatalogSyncHours = request.CatalogSyncHours;
        settings.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return await GetSettingsAsync();
    }

    public async Task MarkSyncCompletedAsync(string cacheKey)
    {
        var hours = await GetCatalogSyncHoursAsync();
        // Catalog cursor must outlive the sync interval so delta runs can read LastRunTimestamp.
        var ttl = string.Equals(cacheKey, CjSyncCacheKeys.CatalogLastSync, StringComparison.Ordinal)
            ? TimeSpan.FromDays(90)
            : TimeSpan.FromHours(Math.Max(hours, 24) * 2);
        await _cache.SetStringAsync(
            cacheKey,
            DateTimeOffset.UtcNow.ToString("O"),
            new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = ttl });
    }

    private async Task<StoreRuntimeSettings> EnsureRowAsync()
    {
        var existing = await _db.StoreRuntimeSettings.FirstOrDefaultAsync(row => row.Id == 1);
        if (existing is not null)
        {
            return existing;
        }

        var seeded = new StoreRuntimeSettings
        {
            Id = 1,
            DefaultMarkup = _configuration.GetValue("StoreSettings:DefaultMarkup", 2.0m),
            CatalogSyncEnabled = _configuration.GetValue("CJDropshipping:CatalogSyncEnabled", true),
            CatalogSyncHours = NormalizeCatalogHours(
                _configuration.GetValue("CJDropshipping:CatalogSyncHours", 6)),
            UpdatedAt = DateTime.UtcNow
        };
        _db.StoreRuntimeSettings.Add(seeded);
        await _db.SaveChangesAsync();
        return seeded;
    }

    private async Task<DateTimeOffset?> ReadTimestampAsync(string key)
    {
        var raw = await _cache.GetStringAsync(key);
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        return DateTimeOffset.TryParse(raw, out var parsed) ? parsed.ToUniversalTime() : null;
    }

    private static int NormalizeCatalogHours(int hours) => hours switch
    {
        24 => 24,
        12 => 12,
        _ => 6,
    };
}
