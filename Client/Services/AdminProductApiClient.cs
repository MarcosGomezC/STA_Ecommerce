using System.Net.Http.Json;
using STA_Ecommerce.Shared;

namespace STA_Ecommerce.Client.Services;

public class AdminProductApiClient
{
    private readonly HttpClient _http;

    public AdminProductApiClient(HttpClient http)
    {
        _http = http;
    }

    public async Task<ProductDetailsDto> FetchProductDetailsAsync(string affiliateUrl)
    {
        var response = await _http.PostAsJsonAsync("api/products/fetch-details", affiliateUrl);
        
        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync();
            throw new HttpRequestException(
                $"Error al obtener detalles: {response.StatusCode} - {errorContent}");
        }
        
        return await response.Content.ReadFromJsonAsync<ProductDetailsDto>() 
            ?? throw new Exception("No se pudo obtener los detalles del producto");
    }

    public async Task<Product> CreateProductAsync(Product product)
    {
        var response = await _http.PostAsJsonAsync("api/products", product);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<Product>() 
            ?? throw new Exception("No se pudo crear el producto");
    }

    public async Task UpdateProductAsync(int id, Product product)
    {
        var response = await _http.PutAsJsonAsync($"api/products/{id}", product);
        response.EnsureSuccessStatusCode();
    }

    public async Task DeleteProductAsync(int id)
    {
        var response = await _http.DeleteAsync($"api/products/{id}");
        response.EnsureSuccessStatusCode();
    }
}

