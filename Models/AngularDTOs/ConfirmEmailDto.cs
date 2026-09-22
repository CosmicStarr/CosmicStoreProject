using System.ComponentModel.DataAnnotations;

namespace Models.AngularDTOs;

public class ConfirmEmailDto
{
    public string? UserId { get; set; }

    [Required]
    public string Token { get; set; } = string.Empty;

    [EmailAddress]
    public string? Email { get; set; }
}
