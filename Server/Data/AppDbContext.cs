using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using STA_Ecommerce.Shared;

namespace STA_Ecommerce.Server.Data;

public class AppDbContext : IdentityDbContext<IdentityUser>
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<Product> Products => Set<Product>();
    public DbSet<ClickTracking> ClickTrackings => Set<ClickTracking>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Configurar índices para mejor rendimiento
        modelBuilder.Entity<Product>(entity =>
        {
            entity.HasIndex(e => e.Proveedor);
            entity.HasIndex(e => e.Categoria);
            entity.HasIndex(e => e.Precio);
        });

        modelBuilder.Entity<ClickTracking>(entity =>
        {
            entity.HasIndex(e => e.ProductId);
            entity.HasIndex(e => e.Timestamp);
            entity.HasIndex(e => new { e.ProductId, e.Timestamp });
        });
    }
}