using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Models;

namespace Data;


public class DbInitializer
{
    public static void InitDb(WebApplication app)
    {
        using var scope = app.Services.CreateScope();

        var DbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>()?? throw new InvalidOperationException("No data to seed!?");

        SeedData(DbContext);
    }

    private static void SeedData(ApplicationDbContext context)
    {
        context.Database.Migrate();

        if(context.GetProducts.Any()) return;

        var someProducts = new List<Products>
        {
            new() {
                Name = "PS5",
                Description = "My Game",
                Price = 599,
                Brand = new Brand{Name = "Sony"},
                Category = new Category{Name = "Electronics"},
                PictureURL = "www.Sony.com",
                Quantity = 10 
            },
            new() {
                Name = "Xbox",
                Description = "My other Game",
                Price = 399,
                Brand = new Brand{Name = "Sony"},
                Category = new Category{Name = "Electronics"},
                PictureURL = "www.Sony.com",
                Quantity = 10 
            },
            new() {
                Name = "PC",
                Description = "My Game for studying stuff",
                Price = 5999,
                Brand = new Brand{Name = "Sony"},
                Category = new Category{Name = "Electronics"},
                PictureURL = "www.Sony.com",
                Quantity = 10 
            },
            new() {
                Name = "TV",
                Description = "My TV for Livingroom",
                Price = 1599,
                Brand = new Brand{Name = "Sony"},
                Category = new Category{Name = "Electronics"},
                PictureURL = "www.Sony.com",
                Quantity = 10 
            },
            new() {
                Name = "PS5",
                Description = "My Game",
                Price = 599,
                Brand = new Brand{Name = "Sony"},
                Category = new Category{Name = "Electronics"},
                PictureURL = "www.Sony.com",
                Quantity = 10 
            },
            new() {
                Name = "PS5",
                Description = "My Game",
                Price = 599,
                Brand = new Brand{Name = "Sony"},
                Category = new Category{Name = "Electronics"},
                PictureURL = "www.Sony.com",
                Quantity = 10 
            },
        };
        
        
        context.AddRange(someProducts);
        context.SaveChanges();
            
    }
}