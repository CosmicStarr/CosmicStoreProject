using Microsoft.Extensions.Hosting;
using Microsoft.EntityFrameworkCore;
using Models;
using System.Text.Json;
using Data;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

/// <summary>
/// Mirrors the CJ catalog into the staging tables.
///
/// The sync walks every third-level category CJ exposes and pages through each one until
/// it runs out of results, so the staging tables end up holding the whole catalogue rather
/// than the first page of a handful of categories.
///
/// Two limits are worth knowing about. CJ caps totalRecords at 6000 per category query, so
/// a category larger than that cannot be fully enumerated through this endpoint. And every
/// listV2 call costs 50 API points, so a full run over all categories is expensive; the
/// MaxProducts and MaxPagesPerCategory settings exist to bound that spend.
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

    /// <summary>
    /// Marks when the catalogue was last synced. Without it every application restart would
    /// kick off another full run, and a full run is expensive in CJ API points.
    /// </summary>
    private const string LastSyncCacheKey = "cj:catalog:last-sync";

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<CJProductSyncWorker> _logger;

    private readonly string _baseUrl;
    private readonly TimeSpan _syncInterval;
    private readonly TimeSpan _requestDelay;
    private readonly bool _syncEnabled;
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
        // Bound as a double so fractional intervals like 0.5 are usable; binding it as an
        // int makes such a value throw during construction and take startup down with it.
        _syncInterval = TimeSpan.FromHours(configuration.GetValue("CJDropshipping:CatalogSyncHours", 6d));
        _requestDelay = TimeSpan.FromMilliseconds(configuration.GetValue("CJDropshipping:RequestDelayMs", 1200));

        _syncEnabled = configuration.GetValue("CJDropshipping:CatalogSyncEnabled", true);
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
        if (!_syncEnabled)
        {
            _logger.LogInformation(
                "CJ catalog sync is disabled (CJDropshipping:CatalogSyncEnabled is false); the worker will not run.");
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RunSyncAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while syncing CJ products.");
            }

            try
            {
                await Task.Delay(_syncInterval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private async Task RunSyncAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Starting CJ catalog sync at: {Time}", DateTimeOffset.Now);

        using var scope = _scopeFactory.CreateScope();
        var authManager = scope.ServiceProvider.GetRequiredService<CjAuthManager>();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var cache = scope.ServiceProvider.GetRequiredService<IDistributedCache>();

        if (await cache.GetStringAsync(LastSyncCacheKey, stoppingToken) is not null)
        {
            _logger.LogInformation(
                "CJ catalog was already synced within the last {Hours}h; skipping this run.",
                _syncInterval.TotalHours);
            return;
        }

        var token = await authManager.GetValidAccessTokenAsync();
        if (string.IsNullOrWhiteSpace(token))
        {
            throw new Exception("CJ access token was empty.");
        }

        var httpClient = _httpClientFactory.CreateClient();
        httpClient.DefaultRequestHeaders.TryAddWithoutValidation("CJ-Access-Token", token);

        var targets = _configuredCategories.Count > 0
            ? _configuredCategories
            : (await FetchAllCategoryIdsAsync(httpClient, stoppingToken))
                .Select(id => new CjCategoryTarget { Id = id })
                .ToList();

        if (targets.Count == 0)
        {
            _logger.LogWarning("CJ returned no categories to sync.");
            return;
        }

        _logger.LogInformation(
            "Syncing {CategoryCount} CJ categories at {PageSize} products per request, up to {PerCategory} products each.",
            targets.Count, _pageSize, _maxProductsPerCategory > 0 ? _maxProductsPerCategory.ToString() : "unlimited");

        // Products are deduplicated globally because CJ lists the same product under
        // several categories, and upserted per category so a long run makes steady
        // progress instead of holding the entire catalogue in memory.
        var seenProductIds = new HashSet<string>();
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
                var (added, updated) = await UpsertCatalogAsync(dbContext, result.Categories, result.Products, stoppingToken);
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
                _logger.LogWarning("CJ rejected the request; abandoning this sync run. Anything already fetched has been saved.");
                return;
            }
        }

        await cache.SetStringAsync(
            LastSyncCacheKey,
            DateTimeOffset.UtcNow.ToString("O"),
            new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = _syncInterval },
            stoppingToken);

        _logger.LogInformation(
            "CJ sync complete: {Added} added, {Updated} updated, {Unique} unique products across {Categories} categories.",
            totalAdded, totalUpdated, seenProductIds.Count, categoriesDone);
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
                    BigImage = product.BigImage,
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
                    existing.BigImage = product.BigImage;
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
