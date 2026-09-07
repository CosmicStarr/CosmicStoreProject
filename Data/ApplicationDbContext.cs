
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Models;

namespace Data;

public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : DbContext(options)
{
    public DbSet<FlatProduct> FlatProducts { get; set; }   
    public DbSet<FlatCategory> FlatCategories { get; set; }
   
}
