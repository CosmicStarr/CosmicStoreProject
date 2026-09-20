using System.ComponentModel.DataAnnotations;

namespace Models.AngularDTOs;

public class RequestEmailChangeDto
{
    [Required]
    public string ReauthToken { get; set; } = string.Empty;

    [Required]
    [EmailAddress]
    public string NewEmail { get; set; } = string.Empty;
}
