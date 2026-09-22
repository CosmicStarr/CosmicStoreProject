namespace Data.Util;

/// <summary>
/// Distinguishes short Identity lockouts (failed passwords) from security lockouts
/// that only an admin may clear (email-change alert / admin lock).
/// </summary>
public static class AccountLockouts
{
    public const int MaxFailedAccessAttempts = 5;
    public static readonly TimeSpan TemporaryDuration = TimeSpan.FromMinutes(15);

    /// <summary>Far-future end date used for permanent security lockouts.</summary>
    public static readonly DateTimeOffset PermanentEnd = DateTimeOffset.MaxValue;

    /// <summary>
    /// Treats any lock lasting more than one day as permanent (security / admin),
    /// as opposed to the 15-minute failed-password lockout.
    /// </summary>
    public static bool IsPermanent(DateTimeOffset? lockoutEnd) =>
        lockoutEnd is { } end && end > DateTimeOffset.UtcNow.AddDays(1);

    public static bool IsActive(DateTimeOffset? lockoutEnd) =>
        lockoutEnd is { } end && end > DateTimeOffset.UtcNow;
}
