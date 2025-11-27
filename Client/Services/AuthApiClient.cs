using System.Net.Http.Json;
using Microsoft.JSInterop;
using STA_Ecommerce.Shared;

namespace STA_Ecommerce.Client.Services;

public class AuthApiClient
{
    private readonly HttpClient _http;
    private readonly IJSRuntime _jsRuntime;
    private const string TokenKey = "sta_auth_token";

    public AuthApiClient(HttpClient http, IJSRuntime jsRuntime)
    {
        _http = http;
        _jsRuntime = jsRuntime;
    }

    public async Task<bool> LoginAsync(string email, string password)
    {
        var response = await _http.PostAsJsonAsync("api/auth/login", new { Email = email, Password = password });
        
        if (!response.IsSuccessStatusCode)
        {
            return false;
        }

        var loginResponse = await response.Content.ReadFromJsonAsync<LoginResponse>();
        if (loginResponse?.Token != null && !string.IsNullOrWhiteSpace(loginResponse.Token))
        {
            try
            {
                await _jsRuntime.InvokeVoidAsync("localStorage.setItem", TokenKey, loginResponse.Token);
                // Verificar que se guardó correctamente
                var savedToken = await _jsRuntime.InvokeAsync<string>("localStorage.getItem", TokenKey);
                if (string.IsNullOrWhiteSpace(savedToken))
                {
                    Console.WriteLine("Error: El token no se guardó correctamente en localStorage");
                    return false;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error al guardar token: {ex.Message}");
                return false;
            }
        }
        else
        {
            Console.WriteLine("Error: No se recibió token del servidor");
            return false;
        }

        return true;
    }

    public async Task LogoutAsync()
    {
        await _jsRuntime.InvokeVoidAsync("localStorage.removeItem", TokenKey);
    }

    public async Task SetAuthHeaderAsync()
    {
        // El handler ya maneja esto automáticamente
        await Task.CompletedTask;
    }
}

public record LoginResponse(string Token, string Email, IEnumerable<string> Roles);


