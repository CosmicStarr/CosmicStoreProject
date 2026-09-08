using Microsoft.Extensions.Hosting;
using Microsoft.EntityFrameworkCore;
using Models;
using System.Text.Json;
using Data;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

public class CJProductSyncWorker : BackgroundService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        NumberHandling = System.Text.Json.Serialization.JsonNumberHandling.AllowReadingFromString
    };

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<CJProductSyncWorker> _logger;
    private readonly CjAuthRequest _authRequest;
    private readonly TimeSpan _syncInterval;

    public CJProductSyncWorker(
        IServiceScopeFactory scopeFactory,
        ILogger<CJProductSyncWorker> logger,
        IOptions<CjAuthRequest> options,
        IConfiguration configuration)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _authRequest = new CjAuthRequest
        {
            ApiKey = options.Value.ApiKey,
            BaseUrl = options.Value.BaseUrl
        };
        _syncInterval = TimeSpan.FromHours(configuration.GetValue("CJDropshipping:CatalogSyncHours", 6));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var targetCategoryIds = new List<string>
        {
            "D28405AE-66C6-42E6-BFF0-D6FDCB5C083C",
            "66D0D817-353B-492E-87A5-024091FF9000",
            "56845C3D-4D9E-4729-B5D4-6D7DE310C031",
            "4336FAFE-B9C9-4673-8706-BCFAE1448DA2",
            "95D9F317-1DB3-4E42-A031-02223215B9C5"
        };

        while (!stoppingToken.IsCancellationRequested)
        {
            _logger.LogInformation("Starting incremental CJ product sync at: {Time}", DateTimeOffset.Now);

            try
            {
                using var scope = _scopeFactory.CreateScope();
                var authManager = scope.ServiceProvider.GetRequiredService<CjAuthManager>();
                var httpClient = scope.ServiceProvider.GetRequiredService<HttpClient>();
                var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

                var token = await authManager.GetValidAccessTokenAsync();
                if (string.IsNullOrWhiteSpace(token))
                {
                    throw new Exception("CJ access token was empty.");
                }

                httpClient.DefaultRequestHeaders.Clear();
                httpClient.DefaultRequestHeaders.TryAddWithoutValidation("CJ-Access-Token", token);

                var (categories, products) = await FetchCatalogAsync(httpClient, targetCategoryIds, stoppingToken);

                if (products.Count == 0)
                {
                    _logger.LogWarning("CJ returned no products for the configured categories.");
                }
                else
                {
                    var (added, updated) = await UpsertCatalogAsync(dbContext, categories, products, stoppingToken);
                    _logger.LogInformation(
                        "CJ sync complete: {Added} added, {Updated} updated across {Categories} categories.",
                        added, updated, categories.Count);
                }
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

    private async Task<(List<FlatCategory> Categories, List<FlatProduct> Products)> FetchCatalogAsync(
        HttpClient httpClient,
        IEnumerable<string> categoryIds,
        CancellationToken stoppingToken)
    {
        var masterCategories = new List<FlatCategory>();
        var masterProducts = new List<FlatProduct>();
        var baseUrl = (_authRequest.BaseUrl ?? "https://developers.cjdropshipping.com/api2.0").TrimEnd('/');

        foreach (var categoryId in categoryIds)
        {
            _logger.LogInformation("Fetching CJ products for category {CategoryId}", categoryId);

            var requestUrl = $"{baseUrl}/v1/product/listV2?page=1&size=50&categoryId={categoryId}";
            var response = await httpClient.GetAsync(requestUrl, stoppingToken);
            var json = await response.Content.ReadAsStringAsync(stoppingToken);

            var apiResult = JsonSerializer.Deserialize<CjResponse>(json, JsonOptions);

            if (apiResult?.Data?.Content is not null)
            {
                masterCategories.AddRange(apiResult.Data.Content
                    .SelectMany(c => c.ProductList?.Select(p => new FlatCategory
                    {
                        CategoryId = p.CategoryId ?? "NO-CATEGORY",
                        CategoryName = p.OneCategoryName ?? "Unknown Category",
                        FullPath = $"{p.OneCategoryName} > {p.TwoCategoryName} > {p.ThreeCategoryName}"
                    }) ?? Enumerable.Empty<FlatCategory>()));

                masterProducts.AddRange(apiResult.Data.Content
                    .SelectMany(c => c.ProductList?.Select(p => new FlatProduct
                    {
                        Id = p.Id ?? Guid.NewGuid().ToString(),
                        NameEn = p.NameEn ?? "Unknown",
                        Sku = p.Sku ?? "Unknown",
                        SellPrice = ParseLowestPrice(p.SellPrice),
                        BigImage = p.BigImage,
                        CategoryId = p.CategoryId ?? "NO-CATEGORY"
                    }) ?? Enumerable.Empty<FlatProduct>()));
            }

            // Be polite to the CJ API between category requests.
            await Task.Delay(1500, stoppingToken);
        }

        return (
            masterCategories.DistinctBy(c => c.CategoryId).ToList(),
            masterProducts.DistinctBy(p => p.Id).ToList());
    }

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
        var existingCategoryIds = await dbContext.FlatCategories
            .Select(c => c.CategoryId)
            .ToListAsync(stoppingToken);

        var newCategories = categories
            .Where(c => !existingCategoryIds.Contains(c.CategoryId))
            .ToList();

        if (newCategories.Count > 0)
        {
            await dbContext.FlatCategories.AddRangeAsync(newCategories, stoppingToken);
            await dbContext.SaveChangesAsync(stoppingToken);
        }

        var incomingIds = products.Select(p => p.Id).ToList();
        var existingProducts = await dbContext.FlatProducts
            .Where(p => incomingIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, stoppingToken);

        var added = 0;
        var updated = 0;

        foreach (var product in products)
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
        return (added, updated);
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
