using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace Models
{
    [Table("Pictures")]
    public class ProductImage
    {
        [Key]
        public int Id { get; set; }

        [Required, StringLength(450)]
        public string ProductId { get; set; } = string.Empty;

        [ForeignKey(nameof(ProductId))]
        [JsonIgnore]
        public Products? Product { get; set; }

        public required string PhotoUrl { get; set; }
        public required string SkuPhoto { get; set; }
        public string? Type { get; set; }
    }
}
