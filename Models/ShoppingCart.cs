using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Models
{
    public class ShoppingCart
    {
        public ShoppingCart()
        {
            
        }
        public ShoppingCart(string ShoppingCartId)
        {
            Id = ShoppingCartId;
        }

        [Key]
        public required string Id { get; set; }
        public List<CartItems> ShoppingCartItems { get; set; } = new();
        public string ClientSecret { get; set; } = string.Empty;
        public string PaymentId { get; set; } = string.Empty;

        // Shipping choice is held on the cart so the PaymentIntent amount and the
        // final order total are computed from the same numbers.
        public string LogisticName { get; set; } = string.Empty;

        [Column(TypeName = "decimal(18,2)")]
        public decimal ShippingCost { get; set; }
    }
}