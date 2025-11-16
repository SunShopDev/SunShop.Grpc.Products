using Microsoft.EntityFrameworkCore;
using SunShop.Grpc.Products.Models;


namespace SunShop.Grpc.Products.Data;

public class ProductsDbContext: DbContext
{
    public ProductsDbContext(DbContextOptions<ProductsDbContext> options)
    : base(options)
    {
    }

    public DbSet<Product> Products { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Product>(entity =>
        {
            entity.HasKey(e => e.Id);
            //entity.Property(e => e.Id).ValueGeneratedOnAdd();
            entity.Property(e => e.Name).IsRequired().HasMaxLength(200);
            entity.Property(e => e.Description).HasMaxLength(1000);
            entity.Property(e => e.Price).IsRequired().HasPrecision(18,2);
            entity.Property(e => e.Stock).IsRequired();
            entity.Property(e => e.Category).IsRequired().HasMaxLength(100);
            entity.Property(e => e.CreatedAt).IsRequired();
        });
    }
}
