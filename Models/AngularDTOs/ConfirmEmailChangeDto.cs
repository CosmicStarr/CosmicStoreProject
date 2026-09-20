using System.ComponentModel.DataAnnotations;

namespace Models.AngularDTOs;

public class ConfirmEmailChangeDto
{
    [Required]
    public string UserId { get; set; } = string.Empty;

    [Required]
    public string Token { get; set; } = string.Empty;

    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;
}
