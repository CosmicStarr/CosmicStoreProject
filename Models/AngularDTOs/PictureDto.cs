namespace Models.AngularDTOs;

public class PictureDto
{
    public int Id { get; set; }
    public string? ProductId { get; set; }
    public string? PhotoUrl { get; set; }
    public string? SkuPhoto { get; set; }
    public int? ProductTypeId { get; set; }
    public ProductTypeDto? ProductType { get; set; }

    /// <summary>True when this row came from a CJ variant refresh and is not on the storefront yet.</summary>
    public bool IsStorefrontDraft { get; set; }
}
