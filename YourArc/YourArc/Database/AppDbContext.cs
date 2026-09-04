using Microsoft.EntityFrameworkCore;

using YourArc.Data;

namespace YourArc.Database;

public class AppDbContext(DbContextOptions<AppDbContext> options)
    : DbContext(options)
{
    public DbSet<User> Users { get; set; }
}