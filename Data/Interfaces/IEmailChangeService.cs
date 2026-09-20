using Data.Classes;

namespace Data.Interfaces;

public interface IEmailChangeService
{
    TimeSpan ReauthLifetime { get; }
    TimeSpan PendingChangeLifetime { get; }

    Task<string> IssueReauthTokenAsync(string userId);
    Task<bool> ConsumeReauthTokenAsync(string userId, string token);
    Task StorePendingChangeAsync(string userId, string oldEmail, string newEmail);
    Task<PendingEmailChange?> GetPendingChangeAsync(string userId);
    Task RemovePendingChangeAsync(string userId);
    Task<string> IssueLockTokenAsync(string userId);
    Task<bool> ValidateLockTokenAsync(string userId, string token);
    Task RemoveLockTokenAsync(string userId);
}
