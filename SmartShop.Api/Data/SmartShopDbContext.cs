using Microsoft.EntityFrameworkCore;
using SmartShop.Api.Models;

namespace SmartShop.Api.Data;

public class SmartShopDbContext : DbContext
{
    public SmartShopDbContext(DbContextOptions<SmartShopDbContext> options) : base(options)
    {
    }

    public DbSet<Product> Products => Set<Product>();
    public DbSet<Review> Reviews => Set<Review>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Product>(entity =>
        {
            entity.ToTable("Product");
            entity.HasData(
                new Product
                {
                    ProductId = 1,
                    ProductName = "Galaxy S25",
                    Category = "Phone",
                    Price = 22_990_000m,
                    Features = "6.2 inch AMOLED, Snapdragon 8 Elite, 50MP camera"
                },
                new Product
                {
                    ProductId = 2,
                    ProductName = "MacBook Air M4",
                    Category = "Laptop",
                    Price = 28_490_000m,
                    Features = "13.6 inch, 16GB RAM, 18 hours battery"
                },
                new Product
                {
                    ProductId = 3,
                    ProductName = "AirPods Pro 3",
                    Category = "Accessory",
                    Price = 6_790_000m,
                    Features = "Active noise cancellation, USB-C, IP57"
                },
                new Product
                {
                    ProductId = 4,
                    ProductName = "Xiaomi Redmi Note 15",
                    Category = "Phone",
                    Price = 6_490_000m,
                    Features = "120Hz display, 5500mAh battery, 108MP camera"
                }
            );
        });

        modelBuilder.Entity<Review>(entity =>
        {
            entity.ToTable("Review");
            entity.HasOne(r => r.Product)
                  .WithMany(p => p.Reviews)
                  .HasForeignKey(r => r.ProductId)
                  .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
