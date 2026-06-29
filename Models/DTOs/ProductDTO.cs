using System;

namespace Models.DTOs;

public class ProductDTO
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public required string Description { get; set; }
    public decimal Price { get; set; }
    public required string Brand { get; set; }
    public required string Category { get; set; }
    public required string PictureURL { get; set; }
    public int Quantity { get; set; }

}
