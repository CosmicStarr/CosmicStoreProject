namespace Models.AngularDTOs;

public class CjProductPreviewRequest
{
    public string? Pid { get; set; }
    public string? Sku { get; set; }
}

public class CjProductLookup
{
    public string Pid { get; set; } = string.Empty;
    public string LookupKind { get; set; } = string.Empty;
    public CjProductDetailsDto? Details { get; set; }
    public IReadOnlyList<CjVariantDto> Variants { get; set; } = [];
}

public class CjProductDetailsDto
{
    public string? Pid { get; set; }
    public string? ProductNameEn { get; set; }
    public string? ProductSku { get; set; }
    public string? ProductImage { get; set; }
    public string? Description { get; set; }
    public string? CategoryName { get; set; }
}

public class CjProductImportDto
{
    public string CjProductId { get; set; } = string.Empty;
    public string NameEn { get; set; } = string.Empty;
    public string Sku { get; set; } = string.Empty;
    public string? DescriptionEn { get; set; }
    public string? ShortDescription { get; set; }
    public string? BigImage { get; set; }
    public string? Category { get; set; }
    public decimal SellPrice { get; set; }
    public IList<CjVariantDto> Variants { get; set; } = new List<CjVariantDto>();
}
