using System.ComponentModel.DataAnnotations;

namespace Models.AngularDTOs;

public class UserAddressDto
{
    public int Id { get; set; }

    [Required]
    public string Label { get; set; } = string.Empty;

    [Required]
    public string FullName { get; set; } = string.Empty;

    [Required]
    public string StreetAddress { get; set; } = string.Empty;

    [Required]
    public string City { get; set; } = string.Empty;

    [Required]
    public string ProvinceOrState { get; set; } = string.Empty;

    [Required]
    [MaxLength(20)]
    public string ZipCode { get; set; } = string.Empty;

    [Required]
    public string CountryCode { get; set; } = string.Empty;

    public bool IsDefault { get; set; }
}
