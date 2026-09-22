namespace Models.AngularDTOs;

public class ProductTypeDto
{
    public int Id { get; set; }
    public string? ProductId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Sku { get; set; } = string.Empty;
    public decimal Price { get; set; }
}
