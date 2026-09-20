using System.ComponentModel.DataAnnotations;

namespace Models.AngularDTOs;

public class ReauthenticateDto
{
    [Required]
    public string CurrentPassword { get; set; } = string.Empty;
}
