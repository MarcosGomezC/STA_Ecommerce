namespace STA_Ecommerce.Shared;

public class ClickTracking
{
    public int Id { get; set; }
    public int ProductId { get; set; }
    public DateTime Timestamp { get; set; }
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
    public string? Referrer { get; set; }
    public string Source { get; set; } = "Direct"; // Home, Category, Search, etc.
    // Propiedad de navegación agregada
    public Product? Product { get; set; }
}

// DTO para evitar ciclos de serialización
public class ClickTrackingDto
{
    public int Id { get; set; }
    public int ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public string? IpAddress { get; set; }
    public string Source { get; set; } = "Direct";
}

public class ClickStats
{
    public int TotalClicks { get; set; }
    public int UniqueIPs { get; set; }
    public Dictionary<string, int> ClicksByProvider { get; set; } = new();
    public Dictionary<string, int> ClicksBySource { get; set; } = new();
    public List<TopProduct> TopProducts { get; set; } = new();
    public Dictionary<string, int> ClicksByHour { get; set; } = new();
}

public class TopProduct
{
    public int ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public int Clicks { get; set; }
    public decimal ConversionRate { get; set; }
}