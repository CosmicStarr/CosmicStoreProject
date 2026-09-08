namespace Models.AngularDTOs;

public class ProductResponseDto
{
    public string? Id { get; set; }
    public string? NameEn { get; set; }
    public string? Sku { get; set; }
    public decimal SellPrice { get; set; }
    public string? BigImage { get; set; }
    public string? Category { get; set; }
    public string? DescriptionEn { get; set; }
    public bool IsFeatured { get; set; }
    public bool IsNewArrival { get; set;}
    public bool IsTopSelling { get; set; }
    public int StockQuantity { get; set; }
    
    // Here is the one-to-many relationship recreated as an array
    public List<PictureDto> Pictures { get; set; } = new List<PictureDto>();
}