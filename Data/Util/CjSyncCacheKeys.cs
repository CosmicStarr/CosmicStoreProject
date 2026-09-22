namespace Data.Util;

/// <summary>Redis keys for CJ background job last-run timestamps (ISO-8601 UTC).</summary>
public static class CjSyncCacheKeys
{
    public const string CatalogLastSync = "cj:catalog:last-sync";
    public const string VariantsLastSync = "cj:variants:last-sync";
    public const string StockLastSync = "cj:stock:last-sync";
    public const string OrdersLastSync = "cj:orders:last-sync";
}
