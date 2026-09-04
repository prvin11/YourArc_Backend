using System.ComponentModel.DataAnnotations;

namespace YourArc.Data;

public class User
{
    public int Id { get; init; }
    
    [StringLength(50)]
    public string Name { get; init; } = string.Empty;
    
    [StringLength(100)]
    public string Email { get; init; } = string.Empty;
    
    [StringLength(255)]
    public string PasswordHash { get; set; } = string.Empty;
}
