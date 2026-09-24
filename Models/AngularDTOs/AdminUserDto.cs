namespace Models.AngularDTOs;

public class AdminUserDto
{
    public string Id { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? UserName { get; set; }
    public IList<string> Roles { get; set; } = [];
    public bool EmailConfirmed { get; set; }
    public bool IsLocked { get; set; }
    /// <summary>True when locked via security alert or admin lock (not a short failed-password lockout).</summary>
    public bool IsPermanentlyLocked { get; set; }
}

public class StoreRuntimeSettingsDto
{
    public decimal DefaultMarkup { get; set; } = 2.0m;
    public bool CatalogSyncEnabled { get; set; } = true;
    public int CatalogSyncHours { get; set; } = 6;
    public DateTimeOffset? CatalogLastSyncAt { get; set; }
    public DateTimeOffset? VariantsLastSyncAt { get; set; }
    public DateTimeOffset? StockLastSyncAt { get; set; }
    public DateTimeOffset? OrdersLastSyncAt { get; set; }
}

public class UpdateStoreRuntimeSettingsRequest
{
    public decimal DefaultMarkup { get; set; }
    public bool CatalogSyncEnabled { get; set; } = true;
    public int CatalogSyncHours { get; set; }
}
