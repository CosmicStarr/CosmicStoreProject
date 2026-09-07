using System.ComponentModel.DataAnnotations;

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
        public required string ClientSecret { get; set; }
        public required string PaymentId { get; set; }
    }
}