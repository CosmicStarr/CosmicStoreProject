using Data.Util;

namespace CosmicStoreAPI.Util;

/// <summary>
/// Fails fast in Production when live-sale configuration is missing or still pointed at localhost/test keys.
/// </summary>
public static class ProductionGuard
{
    public static void EnsureReady(IConfiguration configuration, IHostEnvironment environment)
    {
        if (!environment.IsProduction())
        {
            return;
        }

        var missing = new List<string>();
        Require(configuration, "Store:PublicOrigin", missing);
        Require(configuration, "ConnectionStrings:DefaultConnection", missing);
        Require(configuration, "ConnectionStrings:RedisConnection", missing);
        Require(configuration, "JWT:SecretKey", missing);
        Require(configuration, "Stripe:SecretKey", missing);
        Require(configuration, "Stripe:PublishableKey", missing);
        Require(configuration, "Stripe:WebhookSecret", missing);
        Require(configuration, "Graph:ClientId", missing);
        Require(configuration, "Graph:ClientSecret", missing);
        Require(configuration, "Graph:TenantId", missing);
        Require(configuration, "ReturnPath:SenderEmail", missing, configuration["Graph:SenderEmail"]);
        Require(configuration, "CJDropshipping:ApiKey", missing);

        if (missing.Count > 0)
        {
            throw new InvalidOperationException(
                "Production is missing required settings: " + string.Join(", ", missing)
                + ". Set them as environment variables (Store__PublicOrigin, ConnectionStrings__DefaultConnection, JWT__SecretKey, Stripe__*, Graph__*, CJDropshipping__ApiKey) or the host secret store.");
        }

        var publicOrigin = StoreUrls.PublicOrigin(configuration);
        if (!publicOrigin.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
            || publicOrigin.Contains("localhost", StringComparison.OrdinalIgnoreCase)
            || publicOrigin.Contains("127.0.0.1", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "Store:PublicOrigin must be the live HTTPS storefront URL (not localhost). Example: https://www.yourdomain.com");
        }

        if (GraphMailAuth.IsClientCredentials(configuration) is false)
        {
            throw new InvalidOperationException(
                "Production mail must use Graph:AuthMode=ClientCredentials with Graph:TenantId, Graph:ClientId, Graph:ClientSecret, and ReturnPath:SenderEmail. Delegated Outlook login will not survive a server restart.");
        }

        var tenantId = configuration["Graph:TenantId"]!;
        if (string.Equals(tenantId, "common", StringComparison.OrdinalIgnoreCase)
            || string.Equals(tenantId, "organizations", StringComparison.OrdinalIgnoreCase)
            || string.Equals(tenantId, "consumers", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "Graph:TenantId must be the Azure AD tenant GUID of the mailbox app. Client-credentials cannot use 'common'.");
        }

        var secret = configuration["JWT:SecretKey"]!;
        if (secret.Length < 64)
        {
            throw new InvalidOperationException("JWT:SecretKey must be at least 64 characters in Production.");
        }

        var allowTestStripe = configuration.GetValue("Stripe:AllowTestKeys", false);
        var stripeSecret = configuration["Stripe:SecretKey"]!;
        var stripePublishable = configuration["Stripe:PublishableKey"]!;
        if (!allowTestStripe
            && (stripeSecret.StartsWith("sk_test_", StringComparison.Ordinal)
                || stripePublishable.StartsWith("pk_test_", StringComparison.Ordinal)))
        {
            throw new InvalidOperationException(
                "Production requires live Stripe keys (sk_live_ / pk_live_). Register https://your-domain/api/Payment/webhook in the Stripe Dashboard. Set Stripe:AllowTestKeys=true only for a staging host.");
        }

        var allowLocal = configuration.GetValue("Store:AllowLocalInfrastructure", false);
        var redis = configuration.GetConnectionString("RedisConnection")!;
        var sql = configuration.GetConnectionString("DefaultConnection")!;
        if (!allowLocal
            && (LooksLocal(redis) || LooksLocal(sql)))
        {
            throw new InvalidOperationException(
                "Production SQL and Redis connection strings must not point at localhost. Set Store:AllowLocalInfrastructure=true only for a local Production smoke test.");
        }
    }

    private static void Require(IConfiguration configuration, string key, List<string> missing, string? alternate = null)
    {
        if (string.IsNullOrWhiteSpace(configuration[key]) && string.IsNullOrWhiteSpace(alternate))
        {
            missing.Add(key);
        }
    }

    private static bool LooksLocal(string value) =>
        value.Contains("localhost", StringComparison.OrdinalIgnoreCase)
        || value.Contains("127.0.0.1", StringComparison.OrdinalIgnoreCase)
        || value.Contains("(localdb)", StringComparison.OrdinalIgnoreCase);
}
