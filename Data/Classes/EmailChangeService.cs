using System.Security.Cryptography;
using System.Text;
using Data.Interfaces;

namespace Data.Classes;

/// <summary>
/// Redis-backed secrets for email change: short-lived re-auth, pending address, and lock-account tokens.
/// </summary>
public class EmailChangeService(ICacheService cache) : IEmailChangeService
{
    public TimeSpan ReauthLifetime { get; } = TimeSpan.FromMinutes(10);
    public TimeSpan PendingChangeLifetime { get; } = TimeSpan.FromMinutes(20);
    private static readonly TimeSpan LockTokenLifetime = TimeSpan.FromDays(7);

    public async Task<string> IssueReauthTokenAsync(string userId)
    {
        var token = CreateToken();
        await cache.ObjectToCache(ReauthKey(userId), new StoredSecret { TokenHash = HashToken(token) }, ReauthLifetime);
        return token;
    }

    public async Task<bool> ConsumeReauthTokenAsync(string userId, string token)
    {
        var stored = await cache.GetCachedObject<StoredSecret>(ReauthKey(userId));
        if (stored is null || !TokensMatch(token, stored.TokenHash))
        {
            return false;
        }

        await cache.RemoveData(ReauthKey(userId));
        return true;
    }

    public Task StorePendingChangeAsync(string userId, string oldEmail, string newEmail)
    {
        return cache.ObjectToCache(PendingKey(userId), new PendingEmailChange
        {
            OldEmail = oldEmail,
            NewEmail = newEmail
        }, PendingChangeLifetime);
    }

    public Task<PendingEmailChange?> GetPendingChangeAsync(string userId)
    {
        return cache.GetCachedObject<PendingEmailChange>(PendingKey(userId));
    }

    public Task RemovePendingChangeAsync(string userId)
    {
        return cache.RemoveData(PendingKey(userId));
    }

    public async Task<string> IssueLockTokenAsync(string userId)
    {
        var token = CreateToken();
        await cache.ObjectToCache(LockKey(userId), new StoredSecret { TokenHash = HashToken(token) }, LockTokenLifetime);
        return token;
    }

    public async Task<bool> ValidateLockTokenAsync(string userId, string token)
    {
        var stored = await cache.GetCachedObject<StoredSecret>(LockKey(userId));
        return stored is not null && TokensMatch(token, stored.TokenHash);
    }

    public Task RemoveLockTokenAsync(string userId)
    {
        return cache.RemoveData(LockKey(userId));
    }

    private static string ReauthKey(string userId) => $"email_reauth_{userId}";
    private static string PendingKey(string userId) => $"email_change_{userId}";
    private static string LockKey(string userId) => $"account_lock_{userId}";

    private static string CreateToken() => Convert.ToHexString(RandomNumberGenerator.GetBytes(32));

    private static string HashToken(string token)
    {
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
    }

    private static bool TokensMatch(string provided, string storedHash)
    {
        var providedHash = HashToken(provided);
        var providedBytes = Encoding.UTF8.GetBytes(providedHash);
        var storedBytes = Encoding.UTF8.GetBytes(storedHash);
        return providedBytes.Length == storedBytes.Length
            && CryptographicOperations.FixedTimeEquals(providedBytes, storedBytes);
    }

    private sealed class StoredSecret
    {
        public string TokenHash { get; set; } = string.Empty;
    }
}
