using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;

namespace Models.AngularDTOs
{
    public class RegisterDto
    {
        [Required]
        public string UserName { get; set; } = string.Empty;

        [Required]
        [EmailAddressAttribute(ErrorMessage ="Only valid emails are allowed!")]
        public string Email { get; set; } = string.Empty;

        [Required]
        [StringLength(20, MinimumLength = 8, ErrorMessage = "Password must be 8 to 20 characters.")]
        [RegularExpression(
            "^(?=.*[a-z])(?=.*[A-Z])(?=.*\\d)(?=.*[@$!%*?&])[A-Za-z\\d@$!%*?&]{8,20}$",
            ErrorMessage = "Password must include upper and lower case letters, a number, and a symbol (@ $ ! % * ? &).")]
        [DataType(DataType.Password)]
        public string Password { get; set; } = string.Empty;

        // Ensure this matches the Password property name exactly
        [Required]
        [DataType(DataType.Password)]
        [Compare("Password", ErrorMessage = "The password and confirmation password do not match.")]
        public string ConfirmPassword { get; set; } = string.Empty;
        public ICollection<IdentityError>? RegisterErrors { get; set; }
        public string Token { get; set; } = string.Empty;
        public bool EmailConfirmed { get; set; }
        public bool IsGuest { get; set; }

    }
}