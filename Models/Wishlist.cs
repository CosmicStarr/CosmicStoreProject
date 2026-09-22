using System.ComponentModel.DataAnnotations;

namespace Models;

/// <summary>
/// One saved-product list per user. Gift registries bind a saved address that is never sent to buyers.
/// </summary>
public class Wishlist
{
    [Key]
    public int Id { get; set; }

    [MaxLength(32)]
    public string PublicId { get; set; } = Guid.NewGuid().ToString("N");

    public string AppUserId { get; set; } = string.Empty;

    [MaxLength(80)]
    public string Name { get; set; } = "My Wishlist";

    public int? ShippingAddressId { get; set; }

    public UserAddress? ShippingAddress { get; set; }

    /// <summary>When true, the list is shareable and the delivery address is masked for buyers.</summary>
    public bool IsGiftRegistry { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<WishlistItem> Items { get; set; } = new List<WishlistItem>();
}
