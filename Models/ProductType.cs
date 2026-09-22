using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace Models;

[Table("ProductTypes")]
public class ProductType
{
    [Key]
    public int Id { get; set; }

    [Required, StringLength(450)]
    public string ProductId { get; set; } = string.Empty;

    [ForeignKey(nameof(ProductId))]
    [JsonIgnore]
    public Products? Product { get; set; }

    public string Name { get; set; } = string.Empty;
    public string Sku { get; set; } = string.Empty;

    [Column(TypeName = "decimal(18,2)")]
    public decimal Price { get; set; }

    [JsonIgnore]
    public IList<ProductImage> Images { get; set; } = new List<ProductImage>();
}
