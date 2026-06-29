using System.ComponentModel.DataAnnotations.Schema;

namespace Models;

public class Products
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public required string Description { get; set; }
    [Column(TypeName ="decimal(18,2)")]
    public decimal Price { get; set; }
    public required Brand Brand { get; set; }
    public required Category Category { get; set; }
    public required string PictureURL { get; set; }
    public int Quantity { get; set; }
    
}
