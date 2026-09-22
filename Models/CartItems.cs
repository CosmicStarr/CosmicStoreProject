using System.ComponentModel.DataAnnotations.Schema;

namespace Models
{
    public class CartItems
    {
        public int Id { get; set; }
        public required string Name { get; set; }
        public required string Sku { get; set; }
        [Column(TypeName ="decimal(18,2)")]
        public decimal price { get; set; }
        public int Amount { get; set; }

        /// <summary>When set, this line ships to a gift registry; the buyer never sees the street address.</summary>
        public int? WishlistId { get; set; }
    }
}