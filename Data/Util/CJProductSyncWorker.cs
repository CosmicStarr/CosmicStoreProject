using Microsoft.Extensions.Hosting;
using Microsoft.EntityFrameworkCore;
using Models;
using System.Text.Json;
using Data;
using Data.Interfaces;
using Data.Util;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

/// <summary>
/// Mirrors the CJ catalog into the staging tables using delta sync when possible.
///
/// After the first full bootstrap (category walk), later runs query listV2 with
/// timeStart/timeEnd from Redis <c>cj:catalog:last-sync</c> so only newly listed products
/// are fetched. Published storefront products in that delta also get variant + stock
/// refresh. MaxProducts / MaxPagesPerCategory still bound spend on bootstrap runs.
/// </summary>
public class CJProductSyncWorker : BackgroundService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        NumberHandling = System.Text.Json.Serialization.JsonNumberHandling.AllowReadingFromString
    };

    /// <summary>listV2 rejects a page size above 100.</summary>
    private const int MaxCjPageSize = 100;

    /// <summary>SQL Server caps a query at ~2100 parameters, so ids are matched in blocks.</summary>
    private const int UpsertChunkSize = 500;

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<CJProductSyncWorker> _logger;

    private readonly string _baseUrl;
    private readonly TimeSpan _requestDelay;
    private readonly int _pageSize;
    private readonly int _maxPagesPerCategory;
    private readonly int _maxProducts;
    private readonly int _maxProductsPerCategory;
    private readonly IReadOnlyList<CjCategoryTarget> _configuredCategories;

    public CJProductSyncWorker(
        IServiceScopeFactory scopeFactory,
        IHttpClientFactory httpClientFactory,
        ILogger<CJProductSyncWorker> logger,
        IOptions<CjAuthRequest> options,
        IConfiguration configuration)
    {
        _scopeFactory = scopeFactory;
        _httpClientFactory = httpClientFactory;
        _logger = logger;

        _baseUrl = (options.Value.BaseUrl ?? "https://developers.cjdropshipping.com/api2.0").TrimEnd('/');
        _requestDelay = TimeSpan.FromMilliseconds(configuration.GetValue("CJDropshipping:RequestDelayMs", 1200));

        _pageSize = Math.Clamp(configuration.GetValue("CJDropshipping:CatalogPageSize", MaxCjPageSize), 1, MaxCjPageSize);

        // 0 means "no cap" for all three of these.
        _maxPagesPerCategory = Math.Max(0, configuration.GetValue("CJDropshipping:MaxPagesPerCategory", 0));
        _maxProducts = Math.Max(0, configuration.GetValue("CJDropshipping:MaxProducts", 0));
        _maxProductsPerCategory = Math.Max(0, configuration.GetValue("CJDropshipping:MaxProductsPerCategory", 0));

        // Leave Categories empty to sync every category CJ knows about.
        _configuredCategories = configuration
            .GetSection("CJDropshipping:Categories")
            .Get<List<CjCategoryTarget>>()?
            .Where(target => !string.IsNullOrWhiteSpace(target.Id))
            .ToList()
            ?? [];
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "CJ catalog sync worker started. Enable/disable and interval are controlled from Admin Settings.");

        while (!stoppingToken.IsCancellationRequested)
        {
            var (enabled, syncHours) = await ResolveCatalogSyncSettingsAsync(stoppingToken);

            if (!enabled)
            {
                _logger.LogInformation(
                    "CJ catalog sync is turned off in Admin Settings; checking again in 1 minute.");
                if (!await DelayWithCancelCheckAsync(TimeSpan.FromMinutes(1), stoppingToken))
                {
                    break;
                }

                continue;
            }

            try
            {
                await RunSyncAsync(syncHours, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while syncing CJ products.");
            }

            // Re-read after the run so Settings changes (off, or 6↔12↔24) apply without restart.
            // Wait in short slices so turning the worker off mid-interval takes effect quickly.
            (enabled, syncHours) = await ResolveCatalogSyncSettingsAsync(stoppingToken);
            if (!enabled)
            {
                continue;
            }

            if (!await DelayWithCancelCheckAsync(TimeSpan.FromHours(syncHours), stoppingToken, checkSettings: true))
            {
                break;
            }
        }
    }

    private async Task<(bool Enabled, int Hours)> ResolveCatalogSyncSettingsAsync(CancellationToken stoppingToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var settings = scope.ServiceProvider.GetRequiredService<IStoreSettingsService>();
        var enabled = await settings.IsCatalogSyncEnabledAsync();
        var hours = await settings.GetCatalogSyncHoursAsync();
        stoppingToken.ThrowIfCancellationRequested();
        return (enabled, hours is 24 or 12 ? hours : 6);
    }

    /// <summary>
    /// Delays in 1-minute slices. When <paramref name="checkSettings"/> is true, stops early if sync is turned off.
    /// </summary>
    private async Task<bool> DelayWithCancelCheckAsync(
        TimeSpan delay,
        CancellationToken stoppingToken,
        bool checkSettings = false)
    {
        var remaining = delay;
        while (remaining > TimeSpan.Zero && !stoppingToken.IsCancellationRequested)
        {
            if (checkSettings)
            {
                var (enabled, _) = await ResolveCatalogSyncSettingsAsync(stoppingToken);
                if (!enabled)
                {
                    _logger.LogInformation("CJ catalog sync was turned off during the wait; skipping remaining delay.");
                    return true;
                }
            }

            var slice = remaining > TimeSpan.FromMinutes(1) ? TimeSpan.FromMinutes(1) : remaining;
            try
            {
                await Task.Delay(slice, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                return false;
            }

            remaining -= slice;
        }

        return !stoppingToken.IsCancellationRequested;
    }

    private async Task RunSyncAsync(int syncHours, CancellationToken stoppingToken)
    {
        _logger.LogInformation("Starting CJ catalog sync at: {Time}", DateTimeOffset.Now);

        using var scope = _scopeFactory.CreateScope();
        var authManager = scope.ServiceProvider.GetRequiredService<CjAuthManager>();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var cache = scope.ServiceProvider.GetRequiredService<IDistributedCache>();
        var storeSettings = scope.ServiceProvider.GetRequiredService<IStoreSettingsService>();

        var lastRunRaw = await cache.GetStringAsync(CjSyncCacheKeys.CatalogLastSync, stoppingToken);
        var lastRun = TryParseTimestamp(lastRunRaw);
        var runStartedAt = DateTimeOffset.UtcNow;

        var token = await authManager.GetValidAccessTokenAsync();
        if (string.IsNullOrWhiteSpace(token))
        {
            throw new Exception("CJ access token was empty.");
        }

        var httpClient = _httpClientFactory.CreateClient();
        httpClient.DefaultRequestHeaders.TryAddWithoutValidation("CJ-Access-Token", token);

        var seenProductIds = new HashSet<string>(StringComparer.Ordinal);
        var changedProductIds = new List<string>();
        var totalAdded = 0;
        var totalUpdated = 0;
        var rejected = false;

        if (lastRun is null)
        {
            _logger.LogInformation(
                "No catalog LastRunTimestamp; running full bootstrap sync (interval setting {Hours}h).",
                syncHours);

            var (added, updated, aborted) = await RunFullCatalogSyncAsync(
                httpClient, dbContext, seenProductIds, stoppingToken);
            totalAdded = added;
            totalUpdated = updated;
            rejected = aborted;
            changedProductIds.AddRange(seenProductIds);
        }
        else
        {
            // Small overlap so products listed near the previous cut-off are not missed.
            var timeStart = lastRun.Value.AddMinutes(-5);
            var timeStartMs = timeStart.ToUnixTimeMilliseconds();
            var timeEndMs = runStartedAt.ToUnixTimeMilliseconds();

            _logger.LogInformation(
                "Delta catalog sync since {LastRun:o} (window {StartMs}–{EndMs}).",
                lastRun.Value, timeStartMs, timeEndMs);

            var delta = await FetchDeltaAsync(httpClient, timeStartMs, timeEndMs, seenProductIds, stoppingToken);
            rejected = delta.Rejected;

            if (delta.Products.Count > 0)
            {
                var (added, updated) = await UpsertCatalogAsync(
                    dbContext, delta.Categories, delta.Products, stoppingToken);
                totalAdded = added;
                totalUpdated = updated;
                changedProductIds.AddRange(delta.Products.Select(p => p.Id));
            }

            _logger.LogInformation(
                "Delta fetch complete: {Fetched} product(s), {New} new, {Changed} updated.",
                delta.Products.Count, totalAdded, totalUpdated);
        }

        if (rejected)
        {
            _logger.LogWarning(
                "CJ rejected a catalog request; abandoning this run without advancing LastRunTimestamp. "
                + "Anything already upserted has been saved.");
            return;
        }

        await RefreshPublishedFulfillmentAsync(scope, changedProductIds, stoppingToken);

        await storeSettings.MarkSyncCompletedAsync(CjSyncCacheKeys.CatalogLastSync);

        _logger.LogInformation(
            "CJ catalog sync complete: {Added} added, {Updated} updated, {Unique} unique product(s).",
            totalAdded, totalUpdated, seenProductIds.Count);
    }

    private async Task<(int Added, int Updated, bool Rejected)> RunFullCatalogSyncAsync(
        HttpClient httpClient,
        ApplicationDbContext dbContext,
        HashSet<string> seenProductIds,
        CancellationToken stoppingToken)
    {
        var targets = _configuredCategories.Count > 0
            ? _configuredCategories
            : (await FetchAllCategoryIdsAsync(httpClient, stoppingToken))
                .Select(id => new CjCategoryTarget { Id = id })
                .ToList();

        if (targets.Count == 0)
        {
            _logger.LogWarning("CJ returned no categories to sync.");
            return (0, 0, false);
        }

        _logger.LogInformation(
            "Syncing {CategoryCount} CJ categories at {PageSize} products per request, up to {PerCategory} products each.",
            targets.Count, _pageSize, _maxProductsPerCategory > 0 ? _maxProductsPerCategory.ToString() : "unlimited");

        var totalAdded = 0;
        var totalUpdated = 0;
        var categoriesDone = 0;

        foreach (var target in targets)
        {
            if (stoppingToken.IsCancellationRequested) break;
            if (ReachedProductCap(seenProductIds.Count))
            {
                _logger.LogInformation("Reached the MaxProducts cap of {Cap}; stopping early.", _maxProducts);
                break;
            }

            var result = await FetchCategoryAsync(httpClient, target, seenProductIds, stoppingToken);
            categoriesDone++;

            if (result.Products.Count > 0)
            {
                var (added, updated) = await UpsertCatalogAsync(
                    dbContext, result.Categories, result.Products, stoppingToken);
                totalAdded += added;
                totalUpdated += updated;

                _logger.LogInformation(
                    "Category {Done}/{Total} ({Label}): fetched {Fetched}, {New} new, {Changed} updated (running total {Running}).",
                    categoriesDone, targets.Count, target.Label, result.Products.Count, added, updated, seenProductIds.Count);
            }
            else
            {
                _logger.LogWarning("Category {Label} returned no products.", target.Label);
            }

            if (result.Rejected)
            {
                return (totalAdded, totalUpdated, true);
            }
        }

        return (totalAdded, totalUpdated, false);
    }

    /// <summary>
    /// For delta (or bootstrap) pids that are already on the storefront, refresh CJ variants and stock only.
    /// </summary>
    private async Task RefreshPublishedFulfillmentAsync(
        IServiceScope scope,
        IReadOnlyList<string> productIds,
        CancellationToken stoppingToken)
    {
        if (productIds.Count == 0) return;

        var storeDb = scope.ServiceProvider.GetRequiredService<ApplicationDbStoreContext>();
        var catalogSync = scope.ServiceProvider.GetRequiredService<ICjCatalogSyncService>();

        var distinctIds = productIds
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var publishedIds = new List<string>();
        foreach (var chunk in distinctIds.Chunk(UpsertChunkSize))
        {
            var chunkList = chunk.ToList();
            var matches = await storeDb.GetProducts
                .AsNoTracking()
                .Where(p => chunkList.Contains(p.Id))
                .Select(p => p.Id)
                .ToListAsync(stoppingToken);
            publishedIds.AddRange(matches);
        }

        if (publishedIds.Count == 0)
        {
            _logger.LogInformation("No published storefront products in this catalog delta; skipping variant/stock refresh.");
            return;
        }

        _logger.LogInformation(
            "Refreshing variants and stock for {Count} published product(s) changed since last run.",
            publishedIds.Count);

        var variantsWritten = 0;
        foreach (var productId in publishedIds)
        {
            if (stoppingToken.IsCancellationRequested) break;
            variantsWritten += await catalogSync.SyncVariantsForProductAsync(productId);
            await Task.Delay(500, stoppingToken);
        }

        var stockUpdated = await catalogSync.SyncStockForProductsAsync(publishedIds, stoppingToken);

        _logger.LogInformation(
            "Published delta fulfillment refresh: {Variants} variant row(s), {Stock} stock row(s).",
            variantsWritten, stockUpdated);
    }

    private static DateTimeOffset? TryParseTimestamp(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        return DateTimeOffset.TryParse(raw, out var parsed) ? parsed.ToUniversalTime() : null;
    }

    /// <summary>
    /// Flattens CJ's three-level category tree down to the third-level ids, which are the
    /// only ones listV2 accepts as a filter.
    /// </summary>
    private async Task<IReadOnlyList<string>> FetchAllCategoryIdsAsync(HttpClient httpClient, CancellationToken stoppingToken)
    {
        var response = await httpClient.GetAsync($"{_baseUrl}/v1/product/getCategory", stoppingToken);
        var json = await response.Content.ReadAsStringAsync(stoppingToken);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("CJ getCategory returned {Status}.", (int)response.StatusCode);
            return [];
        }

        CjCategoryResponse? result;
        try
        {
            result = JsonSerializer.Deserialize<CjCategoryResponse>(json, JsonOptions);
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Could not read the CJ category tree.");
            return [];
        }

        var ids = result?.Data?
            .SelectMany(first => first.CategoryFirstList ?? [])
            .SelectMany(second => second.CategorySecondList ?? [])
            .Select(third => third.CategoryId)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Select(id => id!)
            .Distinct()
            .ToList() ?? [];

        // CJ answers rate limits and permission problems with HTTP 200 and an error
        // payload, so its own code and message are the only way to tell what happened.
        if (ids.Count == 0)
        {
            _logger.LogWarning(
                "CJ getCategory returned no categories (CJ code {Code}: {Message}).",
                result?.Code, result?.Message);
        }

        return ids;
    }

    /// <summary>
    /// Pages listV2 filtered by listing time (milliseconds) since the last successful run.
    /// </summary>
    private async Task<CategoryFetchResult> FetchDeltaAsync(
        HttpClient httpClient,
        long timeStartMs,
        long timeEndMs,
        HashSet<string> seenProductIds,
        CancellationToken stoppingToken)
    {
        var categories = new List<FlatCategory>();
        var products = new List<FlatProduct>();

        var page = 1;
        var totalPages = 1;

        while (page <= totalPages && !stoppingToken.IsCancellationRequested)
        {
            if (_maxPagesPerCategory > 0 && page > _maxPagesPerCategory) break;
            if (ReachedProductCap(seenProductIds.Count)) break;

            var requestUrl =
                $"{_baseUrl}/v1/product/listV2?page={page}&size={_pageSize}"
                + $"&timeStart={timeStartMs}&timeEnd={timeEndMs}&features=enable_category";
            var response = await httpClient.GetAsync(requestUrl, stoppingToken);
            var json = await response.Content.ReadAsStringAsync(stoppingToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "CJ listV2 delta returned {Status} for page {Page}; stopping delta pages.",
                    (int)response.StatusCode, page);
                break;
            }

            CjResponse? apiResult;
            try
            {
                apiResult = JsonSerializer.Deserialize<CjResponse>(json, JsonOptions);
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "Could not read CJ listV2 delta page {Page}.", page);
                break;
            }

            if (apiResult?.Data is null)
            {
                _logger.LogWarning(
                    "CJ listV2 delta returned no data for page {Page} (CJ code {Code}: {Message}).",
                    page, apiResult?.Code, apiResult?.Message);

                var rejected = apiResult is not null && !apiResult.Result;
                return new CategoryFetchResult(Dedupe(categories), products, rejected);
            }

            totalPages = apiResult.Data.TotalPages > 0 ? apiResult.Data.TotalPages : 1;

            var pageProducts = apiResult.Data.Content?
                .SelectMany(content => content.ProductList ?? [])
                .ToList() ?? [];

            if (pageProducts.Count == 0) break;

            foreach (var product in pageProducts)
            {
                if (string.IsNullOrWhiteSpace(product.Id)) continue;
                if (!seenProductIds.Add(product.Id)) continue;

                categories.Add(new FlatCategory
                {
                    CategoryId = product.CategoryId?.Trim() ?? "NO-CATEGORY",
                    CategoryName = product.OneCategoryName?.Trim() ?? "Unknown Category",
                    FullPath = $"{product.OneCategoryName?.Trim()} > {product.TwoCategoryName?.Trim()} > {product.ThreeCategoryName?.Trim()}"
                });

                products.Add(new FlatProduct
                {
                    Id = product.Id,
                    NameEn = product.NameEn?.Trim() ?? "Unknown",
                    Sku = product.Sku?.Trim() ?? "Unknown",
                    SellPrice = ParseLowestPrice(product.SellPrice),
                    BigImage = ImageUrlNormalizer.First(product.BigImage),
                    CategoryId = product.CategoryId?.Trim() ?? "NO-CATEGORY"
                });

                if (ReachedProductCap(seenProductIds.Count))
                {
                    return new CategoryFetchResult(Dedupe(categories), products, false);
                }
            }

            page++;

            if (page <= totalPages)
            {
                await Task.Delay(_requestDelay, stoppingToken);
            }
        }

        return new CategoryFetchResult(Dedupe(categories), products, false);
    }

    /// <summary>
    /// Pages through one category until CJ runs out of results, skipping products already
    /// collected under an earlier category.
    /// </summary>
    private async Task<CategoryFetchResult> FetchCategoryAsync(
        HttpClient httpClient,
        CjCategoryTarget target,
        HashSet<string> seenProductIds,
        CancellationToken stoppingToken)
    {
        // A per-category override wins, otherwise fall back to the shared setting.
        var cap = target.MaxProducts > 0 ? target.MaxProducts : _maxProductsPerCategory;

        var result = await FetchByFilterAsync(httpClient, "categoryId", target.Id, cap, seenProductIds, stoppingToken);

        // listV2 matches third-level ids through categoryId and second-level ids through
        // lv2categoryList. An id copied out of a CJ storefront URL can be either, so when a
        // hand-configured category comes back empty, try it as a second-level id instead.
        // Ids discovered from getCategory are always third level and need no second attempt.
        if (result.Products.Count == 0 && !result.Rejected && _configuredCategories.Count > 0)
        {
            _logger.LogInformation(
                "No products for {Label} as a third-level category; retrying it as a second-level category.",
                target.Label);

            result = await FetchByFilterAsync(httpClient, "lv2categoryList", target.Id, cap, seenProductIds, stoppingToken);
        }

        return result;
    }

    /// <param name="cap">Maximum products to take from this category; 0 means no limit.</param>
    private async Task<CategoryFetchResult> FetchByFilterAsync(
        HttpClient httpClient,
        string filterName,
        string categoryId,
        int cap,
        HashSet<string> seenProductIds,
        CancellationToken stoppingToken)
    {
        var categories = new List<FlatCategory>();
        var products = new List<FlatProduct>();

        var page = 1;
        var totalPages = 1;

        while (page <= totalPages && !stoppingToken.IsCancellationRequested)
        {
            if (_maxPagesPerCategory > 0 && page > _maxPagesPerCategory) break;
            if (cap > 0 && products.Count >= cap) break;

            // enable_category is required for oneCategoryName/twoCategoryName/threeCategoryName.
            // Without it CJ returns the category ids but no names, and everything lands in
            // the catalogue as "Unknown Category".
            var requestUrl =
                $"{_baseUrl}/v1/product/listV2?page={page}&size={_pageSize}&{filterName}={categoryId}&features=enable_category";
            var response = await httpClient.GetAsync(requestUrl, stoppingToken);
            var json = await response.Content.ReadAsStringAsync(stoppingToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "CJ listV2 returned {Status} for category {CategoryId} page {Page}; skipping the rest of this category.",
                    (int)response.StatusCode, categoryId, page);
                break;
            }

            CjResponse? apiResult;
            try
            {
                apiResult = JsonSerializer.Deserialize<CjResponse>(json, JsonOptions);
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "Could not read CJ listV2 page {Page} for category {CategoryId}.", page, categoryId);
                break;
            }

            if (apiResult?.Data is null)
            {
                _logger.LogWarning(
                    "CJ listV2 returned no data for category {CategoryId} page {Page} (CJ code {Code}: {Message}).",
                    categoryId, page, apiResult?.Code, apiResult?.Message);

                // A rejected request means the account is out of points, rate limited or
                // disabled. Walking the remaining categories would just repeat the error.
                var rejected = apiResult is not null && !apiResult.Result;
                return new CategoryFetchResult(Dedupe(categories), products, rejected);
            }

            totalPages = apiResult.Data.TotalPages > 0 ? apiResult.Data.TotalPages : 1;

            var pageProducts = apiResult.Data.Content?
                .SelectMany(content => content.ProductList ?? [])
                .ToList() ?? [];

            // CJ occasionally reports more pages than it will actually serve.
            if (pageProducts.Count == 0) break;

            foreach (var product in pageProducts)
            {
                // Without an id there is nothing stable to upsert against, and inventing one
                // would insert a duplicate row on every run.
                if (string.IsNullOrWhiteSpace(product.Id)) continue;
                if (!seenProductIds.Add(product.Id)) continue;

                categories.Add(new FlatCategory
                {
                    CategoryId = product.CategoryId?.Trim() ?? "NO-CATEGORY",
                    CategoryName = product.OneCategoryName?.Trim() ?? "Unknown Category",
                    FullPath = $"{product.OneCategoryName?.Trim()} > {product.TwoCategoryName?.Trim()} > {product.ThreeCategoryName?.Trim()}"
                });

                products.Add(new FlatProduct
                {
                    Id = product.Id,
                    NameEn = product.NameEn?.Trim() ?? "Unknown",
                    Sku = product.Sku?.Trim() ?? "Unknown",
                    SellPrice = ParseLowestPrice(product.SellPrice),
                    BigImage = ImageUrlNormalizer.First(product.BigImage),
                    CategoryId = product.CategoryId?.Trim() ?? "NO-CATEGORY"
                });

                // Stop as soon as this category has contributed its quota, or the run as a
                // whole has hit the overall ceiling.
                if ((cap > 0 && products.Count >= cap) || ReachedProductCap(seenProductIds.Count))
                {
                    return new CategoryFetchResult(Dedupe(categories), products, false);
                }
            }

            page++;

            // listV2 is rate limited and costs 50 API points per call, so pace the loop.
            if (page <= totalPages)
            {
                await Task.Delay(_requestDelay, stoppingToken);
            }
        }

        return new CategoryFetchResult(Dedupe(categories), products, false);
    }

    /// <param name="Rejected">CJ refused the request outright, so the run should stop.</param>
    private sealed record CategoryFetchResult(
        List<FlatCategory> Categories,
        List<FlatProduct> Products,
        bool Rejected);

    private bool ReachedProductCap(int seenCount) => _maxProducts > 0 && seenCount >= _maxProducts;

    private static List<FlatCategory> Dedupe(List<FlatCategory> categories) =>
        categories.DistinctBy(c => c.CategoryId).ToList();

    /// <summary>
    /// Merges the fetched catalog into the staging tables without deleting existing rows, so
    /// products already published to the storefront keep their identifiers.
    /// </summary>
    private static async Task<(int Added, int Updated)> UpsertCatalogAsync(
        ApplicationDbContext dbContext,
        List<FlatCategory> categories,
        List<FlatProduct> products,
        CancellationToken stoppingToken)
    {
        await UpsertCategoriesAsync(dbContext, categories, stoppingToken);

        var added = 0;
        var updated = 0;

        foreach (var chunk in products.Chunk(UpsertChunkSize))
        {
            var chunkIds = chunk.Select(p => p.Id).ToList();
            var existingProducts = await dbContext.FlatProducts
                .Where(p => chunkIds.Contains(p.Id))
                .ToDictionaryAsync(p => p.Id, stoppingToken);

            foreach (var product in chunk)
            {
                if (existingProducts.TryGetValue(product.Id, out var existing))
                {
                    existing.NameEn = product.NameEn;
                    existing.Sku = product.Sku;
                    existing.SellPrice = product.SellPrice;
                    existing.BigImage = ImageUrlNormalizer.First(product.BigImage);
                    existing.CategoryId = product.CategoryId;
                    updated++;
                }
                else
                {
                    await dbContext.FlatProducts.AddAsync(product, stoppingToken);
                    added++;
                }
            }

            await dbContext.SaveChangesAsync(stoppingToken);

            // A full catalogue run touches far too many rows to keep them all tracked.
            dbContext.ChangeTracker.Clear();
        }

        return (added, updated);
    }

    private static async Task UpsertCategoriesAsync(
        ApplicationDbContext dbContext,
        List<FlatCategory> categories,
        CancellationToken stoppingToken)
    {
        if (categories.Count == 0) return;

        var categoryIds = categories.Select(c => c.CategoryId).ToList();
        var existing = await dbContext.FlatCategories
            .Where(c => categoryIds.Contains(c.CategoryId))
            .ToDictionaryAsync(c => c.CategoryId, stoppingToken);

        foreach (var category in categories)
        {
            if (existing.TryGetValue(category.CategoryId, out var current))
            {
                // Refreshes names that were stored before they were trimmed.
                current.CategoryName = category.CategoryName;
                current.FullPath = category.FullPath;
            }
            else
            {
                await dbContext.FlatCategories.AddAsync(category, stoppingToken);
                existing[category.CategoryId] = category;
            }
        }

        await dbContext.SaveChangesAsync(stoppingToken);
        dbContext.ChangeTracker.Clear();
    }

    private static decimal ParseLowestPrice(string? priceString)
    {
        if (string.IsNullOrWhiteSpace(priceString)) return 0m;

        var parts = priceString.Split(['-', '~'], StringSplitOptions.RemoveEmptyEntries);

        return parts.Length > 0 && decimal.TryParse(parts[0].Trim(), out var lowestPrice)
            ? lowestPrice
            : 0m;
    }
}
