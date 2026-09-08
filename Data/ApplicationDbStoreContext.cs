using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Models;
using Models.AngularDTOs;

namespace Data;

public class ApplicationDbStoreContext(DbContextOptions<ApplicationDbStoreContext> options) : IdentityDbContext<AppUser>(options)
{

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        
        // Groups Products and ProductImages under a "store" schema prefix
        modelBuilder.HasDefaultSchema("store"); 

        modelBuilder.Entity<Products>()
        .Property(p => p.Id)
        .ValueGeneratedNever();

        modelBuilder.Entity<Order>()
            .HasMany(o => o.Items)
            .WithOne(i => i.Order)
            .HasForeignKey(i => i.OrderId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<OrderItem>()
            .Property(i => i.PriceAtPurchase)
            .HasColumnType("decimal(18,2)");

        modelBuilder.Entity<WishlistItem>()
            .HasIndex(w => new { w.AppUserId, w.ProductId })
            .IsUnique();

        modelBuilder.Entity<WishlistItem>()
            .HasOne(w => w.Product)
            .WithMany()
            .HasForeignKey(w => w.ProductId)
            .OnDelete(DeleteBehavior.Cascade);

        // Tell EF Core this doesn't have a primary key and isn't a real table
        modelBuilder.Entity<ProductWithPictureDto>().HasNoKey();
    }

    public DbSet<Products> GetProducts { get; set; }
    public DbSet<ProductImage> GetProductImages { get; set; }
    public DbSet<AppUser> GetAppUsers { get; set; }
    public DbSet<Order> Orders { get; set; }
    public DbSet<OrderItem> OrderItems { get; set; }
    public DbSet<ShoppingCartSessionId> ShoppingCartSessions { get; set; }
    public DbSet<UserAddress> UserAddresses { get; set; }
    public DbSet<WishlistItem> WishlistItems { get; set; }
}