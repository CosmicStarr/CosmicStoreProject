using Data.Interfaces;
using Data.Util;
using Microsoft.Extensions.Logging;
using Models;

namespace Data.Classes;

/// <summary>
/// Pulls CJ variants and warehouse stock onto store.ProductVariant for fulfillment (vid + stock).
/// This does not publish new types or images to the storefront. Edit and save the product to do that.
/// </summary>
public class CjCatalogSyncService : ICjCatalogSyncService
{
    private const int BatchSize = 500;

    private readonly IStoreUnitOfWork _storeUnitOfWork;
    private readonly ICJDropshippingService _cjService;
    private readonly ICacheService _cacheService;
    private readonly IStoreSettingsService _storeSettingsService;
    private readonly ILogger<CjCatalogSyncService> _logger;

    public CjCatalogSyncService(
        IStoreUnitOfWork storeUnitOfWork,
        ICJDropshippingService cjService,
        ICacheService cacheService,
        IStoreSettingsService storeSettingsService,
        ILogger<CjCatalogSyncService> logger)
    {
        _storeUnitOfWork = storeUnitOfWork;
        _cjService = cjService;
        _cacheService = cacheService;
        _storeSettingsService = storeSettingsService;
        _logger = logger;
    }

    /// <summary>Fetches CJ variants for one storefront product and upserts ProductVariant rows.</summary>
    public async Task<int> SyncVariantsForProductAsync(string productId)
    {
        var product = await _storeUnitOfWork.Repository<Products>()
            .GetFirstOrDefault(p => p.Id == productId);

        if (product is null) return 0;

        var written = await UpsertVariantsAsync(product);
        await _storeUnitOfWork.Complete();
        if (written > 0)
        {
            await _storeSettingsService.MarkSyncCompletedAsync(CjSyncCacheKeys.VariantsLastSync);
        }

        return written;
    }

    /// <summary>Walks published products and upserts their CJ variants. Paced to stay under CJ rate limits.</summary>
    public async Task<int> SyncAllVariantsAsync(CancellationToken cancellationToken = default)
    {
        var products = await _storeUnitOfWork.Repository<Products>()
            .GetAllParams(new PageParams { PageNumber = 1, PageSize = BatchSize });

        var written = 0;

        foreach (var product in products)
        {
            if (cancellationToken.IsCancellationRequested) break;

            written += await UpsertVariantsAsync(product);

            // CJ rate-limits variant lookups, so pace the loop.
            await Task.Delay(500, cancellationToken);
        }

        if (written > 0)
        {
            await _storeUnitOfWork.Complete();
        }

        await _storeSettingsService.MarkSyncCompletedAsync(CjSyncCacheKeys.VariantsLastSync);
        return written;
    }

    /// <summary>Refreshes stock on each ProductVariant and rolls the sum onto Products.StockQuantity.</summary>
    public async Task<int> SyncStockAsync(CancellationToken cancellationToken = default)
    {
        var variants = (await _storeUnitOfWork.Repository<ProductVariant>()
            .GetAllParams(new PageParams { PageNumber = 1, PageSize = BatchSize }))
            .ToList();

        return await ApplyStockAsync(variants, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<int> SyncStockForProductsAsync(
        IEnumerable<string> productIds,
        CancellationToken cancellationToken = default)
    {
        var ids = productIds
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Select(id => id.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (ids.Count == 0) return 0;

        var variants = new List<ProductVariant>();
        foreach (var productId in ids)
        {
            if (cancellationToken.IsCancellationRequested) break;

            var matches = await _storeUnitOfWork.Repository<ProductVariant>()
                .GetAllParams(
                    new PageParams { PageNumber = 1, PageSize = BatchSize },
                    v => v.ProductId == productId);

            variants.AddRange(matches);
        }

        return await ApplyStockAsync(variants, cancellationToken);
    }

    private async Task<int> ApplyStockAsync(List<ProductVariant> variants, CancellationToken cancellationToken)
    {
        if (variants.Count == 0) return 0;

        var stockByVid = await _cjService.GetVariantStockAsync(variants.Select(v => v.CjVariantId));
        if (stockByVid.Count == 0) return 0;

        var updated = 0;

        foreach (var variant in variants)
        {
            if (cancellationToken.IsCancellationRequested) break;
            if (!stockByVid.TryGetValue(variant.CjVariantId, out var stock)) continue;

            variant.StockQuantity = stock;
            variant.LastSyncedAt = DateTime.UtcNow;
            _storeUnitOfWork.Repository<ProductVariant>().Update(variant);
            updated++;
        }

        foreach (var group in variants.GroupBy(v => v.ProductId))
        {
            var product = await _storeUnitOfWork.Repository<Products>()
                .GetFirstOrDefault(p => p.Id == group.Key);

            if (product is null) continue;

            product.StockQuantity = group.Sum(v => v.StockQuantity);
            _storeUnitOfWork.Repository<Products>().Update(product);
        }

        if (updated > 0)
        {
            await _storeUnitOfWork.Complete();
            await _cacheService.RemoveData("products_all");
            _logger.LogInformation("Synced CJ stock for {Count} variants.", updated);
        }

        await _storeSettingsService.MarkSyncCompletedAsync(CjSyncCacheKeys.StockLastSync);
        return updated;
    }

    /// <summary>
    /// Calls CJ variant/query with product.Id as the pid, then inserts or updates store.ProductVariant rows.
    /// </summary>
    private async Task<int> UpsertVariantsAsync(Products product)
    {
        var remoteVariants = await _cjService.GetProductVariantsAsync(product.Id);
        if (remoteVariants.Count == 0) return 0;

        var repo = _storeUnitOfWork.Repository<ProductVariant>();
        var written = 0;

        foreach (var remote in remoteVariants)
        {
            var existing = await repo.GetFirstOrDefault(v => v.ProductId == product.Id && v.CjVariantId == remote.Vid);

            if (existing is null)
            {
                repo.Add(new ProductVariant
                {
                    ProductId = product.Id,
                    CjVariantId = remote.Vid,
                    Sku = string.IsNullOrWhiteSpace(remote.Sku) ? product.Sku : remote.Sku,
                    VariantName = remote.VariantName,
                    CjPrice = remote.SellPrice,
                    ImageUrl = remote.ImageUrl,
                    LastSyncedAt = DateTime.UtcNow
                });
            }
            else
            {
                existing.Sku = string.IsNullOrWhiteSpace(remote.Sku) ? existing.Sku : remote.Sku;
                existing.VariantName = remote.VariantName ?? existing.VariantName;
                existing.CjPrice = remote.SellPrice;
                existing.ImageUrl = remote.ImageUrl ?? existing.ImageUrl;
                existing.LastSyncedAt = DateTime.UtcNow;
                repo.Update(existing);
            }

            written++;
        }

        // Keep the product-level variant id aligned with the first mapped variant for legacy lookups.
        if (string.IsNullOrWhiteSpace(product.CjVariantId))
        {
            product.CjVariantId = remoteVariants[0].Vid;
            _storeUnitOfWork.Repository<Products>().Update(product);
        }

        return written;
    }
}
