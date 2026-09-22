namespace Models.AngularDTOs;

public class AddCartItemDto
{
    public string ProductId { get; set; } = string.Empty;
    public string? Sku { get; set; }
    public int Quantity { get; set; } = 1;
    public string? CartId { get; set; }
    public int? WishlistId { get; set; }
}
