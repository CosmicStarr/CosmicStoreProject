using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Models
{
    [Table("Pictures")]
    public class ProductImage
    {
        [Key]
        public int Id { get; set; } 
        [Required, StringLength(2083)]
        [ForeignKey("ProductId")]
        public string? ProductId {get; set;}
        public required string PhotoUrl { get; set; }
        public required string SkuPhoto { get; set; }
    }
}