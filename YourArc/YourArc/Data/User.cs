using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace YourArc.Data;

[Index(nameof(Email), IsUnique = true)]
public class User
{
    public int Id { get; set; }
    
    [Required]
    [StringLength(50)]
    public string Name { get; set; } = string.Empty;
    
    [Required]
    [StringLength(100)]
    public string Email { get; set; } = string.Empty;
    
    [Required]
    [StringLength(255)]
    public string PasswordHash { get; set; } = string.Empty;
}

