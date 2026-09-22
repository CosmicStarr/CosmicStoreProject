using Models;

namespace Data.Util;

/// <summary>
/// FTC Mail Order Rule: notify and offer a refund if goods cannot ship within 30 days.
/// CosmicStore flags paid, still-unfulfilled orders at 20 days so ops can act before day 30.
/// </summary>
public static class FtcShippingCompliance
{
    public const int ShipDeadlineDays = 30;
    public const int AttentionDays = 20;

    private static readonly HashSet<string> TerminalOrShipped = new(StringComparer.OrdinalIgnoreCase)
    {
        nameof(Status.Shipped),
        nameof(Status.Delivered),
        nameof(Status.Cancelled),
        nameof(Status.Refunded),
    };

    private static readonly HashSet<string> PaidStatuses = new(StringComparer.OrdinalIgnoreCase)
    {
        nameof(Status.PaymentRecevied),
        nameof(Status.Paid),
    };

    public static bool NeedsAttention(Order order)
    {
        if (!PaidStatuses.Contains(order.PaymentStatus ?? string.Empty))
        {
            return false;
        }

        if (TerminalOrShipped.Contains(order.Status ?? string.Empty))
        {
            return false;
        }

        var age = DateTime.UtcNow - order.CreatedAt.ToUniversalTime();
        return age.TotalDays >= AttentionDays;
    }

    public static int DaysAwaitingFulfillment(Order order)
    {
        var age = DateTime.UtcNow - order.CreatedAt.ToUniversalTime();
        return Math.Max(0, (int)Math.Floor(age.TotalDays));
    }
}
