using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;

namespace Models;

public class AppUser:IdentityUser
{
    /// <summary>When the user last agreed to the storefront Terms and Privacy Policy.</summary>
    public DateTime? AcceptedTermsAt { get; set; }

    [MaxLength(32)]
    public string? AcceptedTermsVersion { get; set; }
}