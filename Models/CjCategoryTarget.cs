namespace Models;

/// <summary>
/// One CJ category the catalog sync should pull from, as configured under
/// CJDropshipping:Categories.
/// </summary>
public class CjCategoryTarget
{
    /// <summary>
    /// CJ category id. This is the GUID in a CJ storefront URL, for example the
    /// E9FDC79A-8365-4CA6-AC23-64D971F08B8B in
    /// /list/wholesale-phones-accessories-l-E9FDC79A-8365-4CA6-AC23-64D971F08B8B.html
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>Only used to keep the sync logs readable.</summary>
    public string? Name { get; set; }

    /// <summary>
    /// How many products to take from this category. Leave at 0 to use
    /// CJDropshipping:MaxProductsPerCategory.
    /// </summary>
    public int MaxProducts { get; set; }

    public string Label => string.IsNullOrWhiteSpace(Name) ? Id : Name!;
}
