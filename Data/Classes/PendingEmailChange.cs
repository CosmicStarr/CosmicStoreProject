namespace Data.Classes;

/// <summary>Temporary email-change request stored in Redis until the new address is verified.</summary>
public sealed class PendingEmailChange
{
    public string OldEmail { get; set; } = string.Empty;
    public string NewEmail { get; set; } = string.Empty;
}
