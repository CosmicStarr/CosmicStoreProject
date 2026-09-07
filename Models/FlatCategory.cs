using System.ComponentModel.DataAnnotations;

namespace Models;

public class FlatCategory
{
    [Key]
    public string CategoryId { get; set; } = string.Empty;
    public string CategoryName { get; set; } = string.Empty;
    public string FullPath { get; set; } = string.Empty; 

    // Navigation property: One category has many products
    
    
}