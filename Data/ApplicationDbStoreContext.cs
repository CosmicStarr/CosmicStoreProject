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

        modelBuilder.Entity<Wishlist>()
            .HasIndex(w => w.PublicId)
            .IsUnique();

        modelBuilder.Entity<Wishlist>()
            .HasIndex(w => w.AppUserId)
            .IsUnique();

        modelBuilder.Entity<Wishlist>()
            .HasOne(w => w.ShippingAddress)
            .WithMany()
            .HasForeignKey(w => w.ShippingAddressId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<WishlistItem>()
            .HasIndex(w => new { w.AppUserId, w.ProductId })
            .IsUnique();

        modelBuilder.Entity<WishlistItem>()
            .HasOne(w => w.Wishlist)
            .WithMany(list => list.Items)
            .HasForeignKey(w => w.WishlistId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<ProductVariant>()
            .HasIndex(v => new { v.ProductId, v.CjVariantId })
            .IsUnique();

        modelBuilder.Entity<ProductVariant>()
            .HasIndex(v => v.Sku);

        modelBuilder.Entity<ProductVariant>()
            .HasOne(v => v.Product)
            .WithMany()
            .HasForeignKey(v => v.ProductId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<WishlistItem>()
            .HasOne(w => w.Product)
            .WithMany()
            .HasForeignKey(w => w.ProductId)
            .OnDelete(DeleteBehavior.Cascade);

        // ProductId is the real FK. Without this, EF invented a shadow ProductsId column
        // and the stored procedures (which join on ProductId) never saw gallery rows.
        modelBuilder.Entity<ProductImage>()
            .HasOne(image => image.Product)
            .WithMany(product => product.ProductImages)
            .HasForeignKey(image => image.ProductId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<ProductType>()
            .HasOne(type => type.Product)
            .WithMany(product => product.ProductTypes)
            .HasForeignKey(type => type.ProductId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<ProductImage>()
            .HasOne(image => image.ProductType)
            .WithMany(type => type.Images)
            .HasForeignKey(image => image.ProductTypeId)
            .OnDelete(DeleteBehavior.NoAction);

        modelBuilder.Entity<ProductType>()
            .HasIndex(type => type.ProductId);

        modelBuilder.Entity<ProductType>()
            .Property(type => type.Price)
            .HasColumnType("decimal(18,2)");

        modelBuilder.Entity<StoreRuntimeSettings>()
            .Property(settings => settings.DefaultMarkup)
            .HasColumnType("decimal(18,2)");

        modelBuilder.Entity<StoreRuntimeSettings>()
            .Property(settings => settings.Id)
            .ValueGeneratedNever();

        modelBuilder.Entity<StoreRuntimeSettings>()
            .ToTable("StoreRuntimeSettings", "store");

        // Tell EF Core this doesn't have a primary key and isn't a real table
        modelBuilder.Entity<ProductWithPictureDto>().HasNoKey();
    }

    public DbSet<Products> GetProducts { get; set; }
    public DbSet<ProductImage> GetProductImages { get; set; }
    public DbSet<ProductType> ProductTypes { get; set; }
    public DbSet<AppUser> GetAppUsers { get; set; }
    public DbSet<Order> Orders { get; set; }
    public DbSet<OrderItem> OrderItems { get; set; }
    public DbSet<ShoppingCartSessionId> ShoppingCartSessions { get; set; }
    public DbSet<UserAddress> UserAddresses { get; set; }
    public DbSet<Wishlist> Wishlists { get; set; }
    public DbSet<WishlistItem> WishlistItems { get; set; }
    public DbSet<ProductVariant> ProductVariants { get; set; }
    public DbSet<StoreRuntimeSettings> StoreRuntimeSettings { get; set; }
}