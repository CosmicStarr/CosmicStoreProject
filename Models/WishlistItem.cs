using System.ComponentModel.DataAnnotations;

namespace Models;

public class WishlistItem
{
    [Key]
    public int Id { get; set; }

    public string AppUserId { get; set; } = string.Empty;

    public string ProductId { get; set; } = string.Empty;

    public DateTime AddedAt { get; set; } = DateTime.UtcNow;

    public Products? Product { get; set; }
}
