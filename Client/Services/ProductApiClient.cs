using System.Net.Http.Json;
using STA_Ecommerce.Shared;

namespace STA_Ecommerce.Client.Services;

public class ProductApiClient
{
    private readonly HttpClient _http;

    public ProductApiClient(HttpClient http)
    {
        _http = http;
    }

    public async Task<List<Product>> GetProductsAsync(string? proveedor = null, string? categoria = null, decimal? minPrecio = null, decimal? maxPrecio = null)
    {
        var query = new List<string>();
        if (!string.IsNullOrWhiteSpace(proveedor)) query.Add($"proveedor={Uri.EscapeDataString(proveedor)}");
        if (!string.IsNullOrWhiteSpace(categoria)) query.Add($"categoria={Uri.EscapeDataString(categoria)}");
        if (minPrecio.HasValue) query.Add($"minPrecio={minPrecio.Value}");
        if (maxPrecio.HasValue) query.Add($"maxPrecio={maxPrecio.Value}");

        var qs = query.Count > 0 ? "?" + string.Join("&", query) : string.Empty;
        var result = await _http.GetFromJsonAsync<List<Product>>($"api/products{qs}");
        return result ?? new List<Product>();
    }
}


