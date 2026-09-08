namespace Models.AngularDTOs;

public class CjBalanceDto
{
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "USD";
    /// <summary>True when the balance sits below the configured alert threshold.</summary>
    public bool IsLow { get; set; }
    public decimal Threshold { get; set; }
}
