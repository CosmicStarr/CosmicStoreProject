using System.ComponentModel.DataAnnotations;

namespace Models.AngularDTOs;

public class LockAccountDto
{
    [Required]
    public string UserId { get; set; } = string.Empty;

    [Required]
    public string Token { get; set; } = string.Empty;
}
