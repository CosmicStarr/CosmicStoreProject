using Azure.Core;
using Azure.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Graph;
using Microsoft.Identity.Client;

namespace Data.Util;

public static class GraphMailAuth
{
    public const string MailSendScope = "https://graph.microsoft.com/Mail.Send";
    public const string OfflineAccessScope = "offline_access";
    public const string RedirectUri = "http://localhost";

    private static readonly string[] DelegatedScopes = [MailSendScope, OfflineAccessScope];
    private static readonly object AppGate = new();
    private static IPublicClientApplication? _publicApp;
    private static string? _publicAppKey;

    public static string TokenCachePath { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "CosmicStore",
        "msal-cache.bin");

    public static string AccountIdPath { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "CosmicStore",
        "msal-account-id.txt");

    public static bool IsClientCredentials(IConfiguration configuration) =>
        string.Equals(configuration["Graph:AuthMode"], "ClientCredentials", StringComparison.OrdinalIgnoreCase);

    public static async Task ConnectInteractiveAsync(IConfiguration configuration, ILogger logger, CancellationToken cancellationToken = default)
    {
        if (IsClientCredentials(configuration))
        {
            throw new InvalidOperationException(
                "Graph:AuthMode is ClientCredentials. That mode uses Graph:ClientSecret and does not need graph-auth.");
        }

        var app = GetPublicApp(configuration);
        Console.WriteLine("A browser window will open. Sign in as CosmicStore46@outlook.com.");
        Console.WriteLine("Keep this window open until you see: Saved Microsoft Graph login.");
        logger.LogInformation("Opening a browser for Microsoft Graph mail login.");

        var result = await app.AcquireTokenInteractive(DelegatedScopes)
            .WithUseEmbeddedWebView(false)
            .WithPrompt(Prompt.SelectAccount)
            .ExecuteAsync(cancellationToken);

        Directory.CreateDirectory(Path.GetDirectoryName(AccountIdPath)!);
        await File.WriteAllTextAsync(AccountIdPath, result.Account.HomeAccountId.Identifier, cancellationToken);

        Console.WriteLine($"Saved Microsoft Graph login for {result.Account.Username}.");
        logger.LogInformation(
            "Saved Microsoft Graph login for {User}. Emails will send as this Outlook account.",
            result.Account.Username);
    }

    public static GraphServiceClient CreateClient(IConfiguration configuration, ILogger logger)
    {
        if (IsClientCredentials(configuration))
        {
            var tenantId = Required(configuration, "Graph:TenantId");
            var clientId = Required(configuration, "Graph:ClientId");
            var clientSecret = Required(configuration, "Graph:ClientSecret");
            var credential = new ClientSecretCredential(tenantId, clientId, clientSecret);
            return new GraphServiceClient(credential, ["https://graph.microsoft.com/.default"]);
        }

        if (!File.Exists(TokenCachePath) || !File.Exists(AccountIdPath))
        {
            throw new InvalidOperationException(
                "Microsoft Graph is not signed in. From the repo root run: .\\Scripts\\connect-outlook-graph.ps1");
        }

        return new GraphServiceClient(new MsalSilentTokenCredential(configuration), DelegatedScopes);
    }

    private static IPublicClientApplication GetPublicApp(IConfiguration configuration)
    {
        var clientId = Required(configuration, "Graph:ClientId");
        var tenantId = string.IsNullOrWhiteSpace(configuration["Graph:TenantId"])
            ? "common"
            : configuration["Graph:TenantId"]!;
        var key = $"{clientId}|{tenantId}";

        lock (AppGate)
        {
            if (_publicApp is not null && _publicAppKey == key)
            {
                return _publicApp;
            }

            _publicApp = PublicClientApplicationBuilder.Create(clientId)
                .WithAuthority(AzureCloudInstance.AzurePublic, tenantId)
                .WithRedirectUri(RedirectUri)
                .Build();

            BindTokenCache(_publicApp.UserTokenCache);
            _publicAppKey = key;
            return _publicApp;
        }
    }

    private static void BindTokenCache(ITokenCache tokenCache)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(TokenCachePath)!);

        tokenCache.SetBeforeAccess(args =>
        {
            if (!File.Exists(TokenCachePath))
            {
                return;
            }

            args.TokenCache.DeserializeMsalV3(File.ReadAllBytes(TokenCachePath));
        });

        tokenCache.SetAfterAccess(args =>
        {
            if (!args.HasStateChanged)
            {
                return;
            }

            File.WriteAllBytes(TokenCachePath, args.TokenCache.SerializeMsalV3());
        });
    }

    private static string Required(IConfiguration configuration, string key)
    {
        var value = configuration[key];
        if (!string.IsNullOrWhiteSpace(value))
        {
            return value;
        }

        throw new InvalidOperationException(
            $"{key} is not configured. Store it with: dotnet user-secrets set \"{key}\" \"<value>\"");
    }

    private sealed class MsalSilentTokenCredential(IConfiguration configuration) : TokenCredential
    {
        public override AccessToken GetToken(TokenRequestContext requestContext, CancellationToken cancellationToken) =>
            GetTokenAsync(requestContext, cancellationToken).AsTask().GetAwaiter().GetResult();

        public override async ValueTask<AccessToken> GetTokenAsync(TokenRequestContext requestContext, CancellationToken cancellationToken)
        {
            var app = GetPublicApp(configuration);
            var accountId = (await File.ReadAllTextAsync(AccountIdPath, cancellationToken)).Trim();
            var account = await app.GetAccountAsync(accountId)
                ?? throw new InvalidOperationException(
                    "Microsoft Graph login expired. From the repo root run: .\\Scripts\\connect-outlook-graph.ps1");

            var scopes = requestContext.Scopes is { Length: > 0 } requested
                ? requested.ToArray()
                : DelegatedScopes;

            try
            {
                var result = await app.AcquireTokenSilent(scopes, account)
                    .ExecuteAsync(cancellationToken)
                    .ConfigureAwait(false);
                return new AccessToken(result.AccessToken, result.ExpiresOn);
            }
            catch (MsalUiRequiredException ex)
            {
                throw new InvalidOperationException(
                    "Microsoft Graph login expired. From the repo root run: .\\Scripts\\connect-outlook-graph.ps1",
                    ex);
            }
        }
    }
}
