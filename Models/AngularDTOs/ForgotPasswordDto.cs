using System.ComponentModel.DataAnnotations;

namespace Models.AngularDTOs;

public class ForgotPasswordDto
{
    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;
}
