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
        public string ClientSecret { get; set; } = string.Empty;
        public string PaymentId { get; set; } = string.Empty;
    }
}