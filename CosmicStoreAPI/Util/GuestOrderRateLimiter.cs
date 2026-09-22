using Data.Interfaces;

namespace CosmicStoreAPI.Util;

/// <summary>
/// Redis throttle for the public guest order-verify endpoint.
/// Locks an IP+order after 5 failed checks, and the IP after 20 failures, for 15 minutes.
/// </summary>
public class GuestOrderRateLimiter(ICacheService cache)
{
    public const int MaxOrderFailures = 5;
    public const int MaxIpFailures = 20;
    public static readonly TimeSpan Window = TimeSpan.FromMinutes(15);

    public async Task<bool> IsBlockedAsync(string ip, string? orderId)
    {
        var orderLock = await cache.GetCachedObject<bool>(OrderLockKey(ip, orderId));
        if (orderLock)
        {
            return true;
        }

        return await cache.GetCachedObject<bool>(IpLockKey(ip));
    }

    /// <summary>Records a failed verification. Returns true when this attempt tripped a lock.</summary>
    public async Task<bool> RegisterFailureAsync(string ip, string? orderId)
    {
        var orderFails = await cache.IncrementAsync(OrderFailKey(ip, orderId), Window);
        if (orderFails >= MaxOrderFailures)
        {
            await cache.ObjectToCache(OrderLockKey(ip, orderId), true, Window);
        }

        var ipFails = await cache.IncrementAsync(IpFailKey(ip), Window);
        if (ipFails >= MaxIpFailures)
        {
            await cache.ObjectToCache(IpLockKey(ip), true, Window);
        }

        return orderFails >= MaxOrderFailures || ipFails >= MaxIpFailures;
    }

    private static string Sanitize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "missing";
        }

        var trimmed = value.Trim();
        return trimmed.Length <= 80 ? trimmed : trimmed[..80];
    }

    private static string OrderFailKey(string ip, string? orderId) =>
        $"guest-order:fail:{Sanitize(ip)}:{Sanitize(orderId)}";

    private static string OrderLockKey(string ip, string? orderId) =>
        $"guest-order:lock:{Sanitize(ip)}:{Sanitize(orderId)}";

    private static string IpFailKey(string ip) => $"guest-order:ip-fail:{Sanitize(ip)}";

    private static string IpLockKey(string ip) => $"guest-order:ip-lock:{Sanitize(ip)}";
}
