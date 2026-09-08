using System.ComponentModel.DataAnnotations;

namespace Models;

public class UserAddress
{
    [Key]
    public int Id { get; set; }

    public string AppUserId { get; set; } = string.Empty;

    public string Label { get; set; } = string.Empty;

    public string FullName { get; set; } = string.Empty;

    public string StreetAddress { get; set; } = string.Empty;

    public string City { get; set; } = string.Empty;

    public string ProvinceOrState { get; set; } = string.Empty;

    public string CountryCode { get; set; } = string.Empty;

    public bool IsDefault { get; set; }
}
