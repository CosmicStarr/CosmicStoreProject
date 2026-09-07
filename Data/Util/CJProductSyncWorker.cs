using Microsoft.Extensions.Hosting;
using Microsoft.EntityFrameworkCore;
using Models;
using System.Text.Json;
using Data;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

public class CJProductSyncWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<CJProductSyncWorker> _logger;
    private readonly CjAuthRequest _authRequest;
    public CJProductSyncWorker(IServiceScopeFactory scopeFactory, ILogger<CJProductSyncWorker> logger, IOptions<CjAuthRequest> options)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        var authRequest = new CjAuthRequest
        {
            ApiKey = options.Value.ApiKey,
            BaseUrl = options.Value.BaseUrl
        };
        _authRequest = authRequest;
    }

 protected override async Task ExecuteAsync(CancellationToken stoppingToken)
{
    // Define the specific CJ Category IDs you want to sync
    // You can find these IDs by making a broad request first, or using their Category API
    var targetCategoryIds = new List<string>
    {
        "D28405AE-66C6-42E6-BFF0-D6FDCB5C083C", // e.g., Electronics
        "66D0D817-353B-492E-87A5-024091FF9000",
        "56845C3D-4D9E-4729-B5D4-6D7DE310C031",
        "4336FAFE-B9C9-4673-8706-BCFAE1448DA2",
        "95D9F317-1DB3-4E42-A031-02223215B9C5"  // e.g., Home & Garden
    };

    while (!stoppingToken.IsCancellationRequested)
    {
        _logger.LogInformation("Starting Targeted CJ Product Sync at: {time}", DateTimeOffset.Now);

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var authManager = scope.ServiceProvider.GetRequiredService<CjAuthManager>();
            var httpClient = scope.ServiceProvider.GetRequiredService<HttpClient>();
            var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            var token = await authManager.GetValidAccessTokenAsync();

            if (string.IsNullOrWhiteSpace(token)) 
            {
                throw new Exception("The Access Token is physically empty in C#!");
            }

            httpClient.DefaultRequestHeaders.Clear();
            httpClient.DefaultRequestHeaders.TryAddWithoutValidation("CJ-Access-Token", token);

            // Create master lists to accumulate data from all category requests
            var masterCategories = new List<FlatCategory>();
            var masterProducts = new List<FlatProduct>();

            var options = new JsonSerializerOptions 
            { 
                PropertyNameCaseInsensitive = true,
                NumberHandling = System.Text.Json.Serialization.JsonNumberHandling.AllowReadingFromString 
            };

            // 1. Loop through your target categories
            foreach (var categoryId in targetCategoryIds)
            {
                _logger.LogInformation($"Fetching products for Category ID: {categoryId}");

                // Append the categoryId query parameter. Increased size to 50 for better data batching.
                var requestUrl = $"https://developers.cjdropshipping.com/api2.0/v1/product/listV2?page=1&size=50&categoryId={categoryId}";
                var response = await httpClient.GetAsync(requestUrl, stoppingToken);
                var json = await response.Content.ReadAsStringAsync(stoppingToken);

                var apiResult = JsonSerializer.Deserialize<CjResponse>(json, options);

                if (apiResult?.Data?.Content != null)
                {
                    // Extract categories for this batch
                    var batchCategories = apiResult.Data.Content
                        .SelectMany(c => c.ProductList?.Select(p => new FlatCategory
                        {
                            CategoryId = p.CategoryId ?? "NO-CATEGORY",
                            CategoryName = p.OneCategoryName ?? "Unknown Category",
                            FullPath = $"{p.OneCategoryName} > {p.TwoCategoryName} > {p.ThreeCategoryName}"
                        }) ?? Enumerable.Empty<FlatCategory>())
                        .ToList();

                    masterCategories.AddRange(batchCategories);

                    // Extract products for this batch
                    var batchProducts = apiResult.Data.Content
                        .SelectMany(c => c.ProductList?.Select(p => new FlatProduct
                        {
                            Id = p.Id ?? Guid.NewGuid().ToString(),
                            NameEn = p.NameEn ?? "Unknown",
                            Sku = p.Sku ?? "Unknown",
                            SellPrice = ParseLowestPrice(p.SellPrice),
                            BigImage = p.BigImage,
                            // Temporarily store the string ID, we will map the actual object reference later
                            CategoryId = p.CategoryId ?? "NO-CATEGORY" 
                        }) ?? Enumerable.Empty<FlatProduct>())
                        .ToList();

                    masterProducts.AddRange(batchProducts);
                }

                // Be polite to the CJ API and wait 1.5 seconds between category requests
                await Task.Delay(1500, stoppingToken);
            }

            // 2. Clean up the accumulated data (Remove duplicates across categories)
            var finalUniqueCategories = masterCategories
                .DistinctBy(c => c.CategoryId)
                .ToList();

            var finalUniqueProducts = masterProducts
                .DistinctBy(p => p.Id)
                .ToList();

            // Map the Category objects to the products using the final distinct categories
            foreach (var product in finalUniqueProducts)
            {
                // Ensure your FlatProduct model has a CategoryId string property to hold this temporarily
                product.Category = finalUniqueCategories.FirstOrDefault(c => c.CategoryId == product.CategoryId);
            }

            // 3. Database Wipe and Insert Operations
            if (finalUniqueProducts.Any())
            {
                // Wipe old data
                dbContext.FlatProducts.RemoveRange(dbContext.FlatProducts);
                dbContext.FlatCategories.RemoveRange(dbContext.FlatCategories); 
                await dbContext.SaveChangesAsync(stoppingToken); 

                // Clear tracking memory
                dbContext.ChangeTracker.Clear();

                // Insert new data
                await dbContext.FlatCategories.AddRangeAsync(finalUniqueCategories, stoppingToken);
                await dbContext.SaveChangesAsync(stoppingToken);

                await dbContext.FlatProducts.AddRangeAsync(finalUniqueProducts, stoppingToken);
                await dbContext.SaveChangesAsync(stoppingToken);
                
                _logger.LogInformation($"Successfully synced {finalUniqueProducts.Count} products across {targetCategoryIds.Count} categories.");
            }
            else
            {
                _logger.LogWarning("API returned no products for the specified categories.");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error occurred while syncing CJ products.");
        }

        // Sleep for 6 hours before running again
        await Task.Delay(TimeSpan.FromHours(6), stoppingToken);
    }
}

    private decimal ParseLowestPrice(string? priceString)
    {
        if (string.IsNullOrWhiteSpace(priceString)) return 0m;

        // If it's a range like "12.34-15.00" or "12.34~15.00", split it and take the first part
        var parts = priceString.Split(new[] { '-', '~' }, StringSplitOptions.RemoveEmptyEntries);
        
        if (parts.Length > 0 && decimal.TryParse(parts[0].Trim(), out decimal lowestPrice))
        {
            return lowestPrice;
        }
        
        return 0m;
    }
}