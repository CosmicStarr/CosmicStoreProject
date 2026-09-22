using Data.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Data.Util;

/// <summary>
/// Polls CJ for fulfilment updates on orders that have been submitted but not yet delivered,
/// and refreshes warehouse stock on a slower cadence.
/// </summary>
public class CjOrderStatusWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<CjOrderStatusWorker> _logger;
    private readonly TimeSpan _pollInterval;
    private readonly int _stockSyncEveryNPolls;

    public CjOrderStatusWorker(
        IServiceScopeFactory scopeFactory,
        ILogger<CjOrderStatusWorker> logger,
        IConfiguration configuration)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _pollInterval = TimeSpan.FromMinutes(configuration.GetValue("CJDropshipping:OrderPollMinutes", 30));
        _stockSyncEveryNPolls = configuration.GetValue("CJDropshipping:StockSyncEveryNPolls", 8);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var pollCount = 0;

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var orderService = scope.ServiceProvider.GetRequiredService<IOrderService>();
                var storeSettings = scope.ServiceProvider.GetRequiredService<IStoreSettingsService>();

                var updated = await orderService.SyncPendingOrdersAsync(stoppingToken);
                await storeSettings.MarkSyncCompletedAsync(CjSyncCacheKeys.OrdersLastSync);
                if (updated > 0)
                {
                    _logger.LogInformation("Updated {Count} order(s) from CJ.", updated);
                }

                if (_stockSyncEveryNPolls > 0 && pollCount % _stockSyncEveryNPolls == 0)
                {
                    var catalogSync = scope.ServiceProvider.GetRequiredService<ICjCatalogSyncService>();
                    await catalogSync.SyncStockAsync(stoppingToken);
                }

                var editProducts = scope.ServiceProvider.GetRequiredService<IEditCjProducts>();
                var expired = await editProducts.ExpireStaleNewArrivalsAsync();
                if (expired > 0)
                {
                    _logger.LogInformation("Cleared New Arrival on {Count} product(s) older than 7 days.", expired);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "CJ order status sync failed.");
            }

            pollCount++;

            try
            {
                await Task.Delay(_pollInterval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }
}
