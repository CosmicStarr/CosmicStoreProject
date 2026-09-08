namespace Data.Interfaces;

public interface ICjCatalogSyncService
{
    /// <summary>
    /// Pulls CJ variants for a published product and upserts the SKU to variant-id mapping.
    /// Returns the number of variant rows written.
    /// </summary>
    Task<int> SyncVariantsForProductAsync(string productId);

    /// <summary>Syncs variant mappings for every published storefront product.</summary>
    Task<int> SyncAllVariantsAsync(CancellationToken cancellationToken = default);

    /// <summary>Refreshes stock counts from CJ for all mapped variants and rolls them up onto products.</summary>
    Task<int> SyncStockAsync(CancellationToken cancellationToken = default);
}
