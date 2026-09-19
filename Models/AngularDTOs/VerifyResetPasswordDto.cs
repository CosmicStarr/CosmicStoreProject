using System.ComponentModel.DataAnnotations;

namespace Models.AngularDTOs;

public class VerifyResetPasswordDto
{
    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required]
    public string Token { get; set; } = string.Empty;
}
