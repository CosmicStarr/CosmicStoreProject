namespace Models.AngularDTOs;

// This class matches the exact columns returned by your stored procedure
public class ProductWithPictureDto
{
    public string? Id { get; set; }
    public string? NameEn { get; set; }
    public string? Sku { get; set; }
    public string? DescriptionEn { get; set; }
    public bool IsFeatured { get; set; }
    public bool IsNewArrival { get; set;}
    public bool IsTopSelling { get; set; }
    public decimal SellPrice { get; set; }
    public string? BigImage { get; set; }
    public string? Category { get; set; }
    public string? PictureId { get; set; }
    public string? PhotoUrl { get; set; }
    public string? SkuPhoto { get; set; }
}