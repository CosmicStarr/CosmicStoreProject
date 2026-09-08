using System.ComponentModel.DataAnnotations;

namespace Models.AngularDTOs;

public class UpdateProfileDto
{
    [Required]
    [StringLength(50, MinimumLength = 2)]
    public string UserName { get; set; } = string.Empty;
}
