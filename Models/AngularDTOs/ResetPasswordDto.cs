using System.ComponentModel.DataAnnotations;

namespace Models.AngularDTOs;

public class ResetPasswordDto
{
    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required]
    public string Token { get; set; } = string.Empty;

    [Required]
    [StringLength(20, MinimumLength = 8, ErrorMessage = "Password must be 8 to 20 characters.")]
    [RegularExpression(
        "^(?=.*[a-z])(?=.*[A-Z])(?=.*\\d)(?=.*[@$!%*?&])[A-Za-z\\d@$!%*?&]{8,20}$",
        ErrorMessage = "Password must include upper and lower case letters, a number, and a symbol (@ $ ! % * ? &).")]
    public string NewPassword { get; set; } = string.Empty;

    [Required]
    [Compare(nameof(NewPassword))]
    public string ConfirmPassword { get; set; } = string.Empty;
}
