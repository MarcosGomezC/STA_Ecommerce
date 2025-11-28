using Microsoft.EntityFrameworkCore;
using STA_Ecommerce.Server.Data;
using STA_Ecommerce.Shared;

namespace STA_Ecommerce.Server.Services;

public interface IClickTrackingService
{
    Task TrackClickAsync(int productId, string? ipAddress, string? userAgent, string? referrer, string source);
    Task<ClickStats> GetStatsAsync(DateTime? from = null, DateTime? to = null);
    Task<List<ClickTracking>> GetRecentClicksAsync(int count = 50);
}

public class ClickTrackingService : IClickTrackingService
{
    private readonly AppDbContext _db;
    private readonly ILogger<ClickTrackingService> _logger;

    public ClickTrackingService(AppDbContext db, ILogger<ClickTrackingService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task TrackClickAsync(int productId, string? ipAddress, string? userAgent, string? referrer, string source)
    {
        try
        {
            var click = new ClickTracking
            {
                ProductId = productId,
                Timestamp = DateTime.UtcNow,
                IpAddress = ipAddress,
                UserAgent = userAgent,
                Referrer = referrer,
                Source = source
            };

            _db.ClickTrackings.Add(click);
            await _db.SaveChangesAsync();

            _logger.LogInformation("Click tracked: Product {ProductId}, Source {Source}, IP {IP}", 
                productId, source, ipAddress);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al rastrear click para producto {ProductId}", productId);
        }
    }

    public async Task<ClickStats> GetStatsAsync(DateTime? from = null, DateTime? to = null)
    {
        from ??= DateTime.UtcNow.AddDays(-30);
        to ??= DateTime.UtcNow;

        var clicks = await _db.ClickTrackings
            .Include(c => c.Product)
            .Where(c => c.Timestamp >= from && c.Timestamp <= to)
            .ToListAsync();

        var stats = new ClickStats
        {
            TotalClicks = clicks.Count,
            UniqueIPs = clicks.Select(c => c.IpAddress).Distinct().Count(),
            ClicksByProvider = clicks
                .Where(c => c.Product != null)
                .GroupBy(c => c.Product!.Proveedor)
                .ToDictionary(g => g.Key, g => g.Count()),
            ClicksBySource = clicks
                .GroupBy(c => c.Source)
                .ToDictionary(g => g.Key, g => g.Count()),
            TopProducts = clicks
                .Where(c => c.Product != null)
                .GroupBy(c => new { c.ProductId, c.Product!.Nombre })
                .Select(g => new TopProduct
                {
                    ProductId = g.Key.ProductId,
                    ProductName = g.Key.Nombre,
                    Clicks = g.Count(),
                    ConversionRate = 0 // Por ahora, calcular después con ventas reales
                })
                .OrderByDescending(p => p.Clicks)
                .Take(10)
                .ToList(),
            ClicksByHour = clicks
                .GroupBy(c => c.Timestamp.Hour)
                .ToDictionary(g => g.Key.ToString("D2") + ":00", g => g.Count())
        };

        return stats;
    }

    public async Task<List<ClickTracking>> GetRecentClicksAsync(int count = 50)
    {
        return await _db.ClickTrackings
            .OrderByDescending(c => c.Timestamp)
            .Take(count)
            .ToListAsync();
    }
}