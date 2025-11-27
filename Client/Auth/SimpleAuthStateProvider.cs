using System.Security.Claims;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Components.Authorization;
using STA_Ecommerce.Client.Services;

namespace STA_Ecommerce.Client.Auth;

public class SimpleAuthStateProvider : AuthenticationStateProvider
{
    private readonly HttpClient _http;
    private readonly AuthApiClient _authApi;

    public SimpleAuthStateProvider(HttpClient http, AuthApiClient authApi)
    {
        _http = http;
        _authApi = authApi;
    }

    public override async Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        try
        {
            // Asegurar que el header de autorización esté configurado
            await _authApi.SetAuthHeaderAsync();
            
            var info = await _http.GetFromJsonAsync<AuthInfo>("api/auth/me");
            if (info is { IsAuthenticated: true })
            {
                var claims = new List<Claim>
                {
                    new(ClaimTypes.Name, info.Email ?? string.Empty)
                };

                foreach (var role in info.Roles)
                {
                    claims.Add(new Claim(ClaimTypes.Role, role));
                }

                var identity = new ClaimsIdentity(claims, "jwt");
                var user = new ClaimsPrincipal(identity);
                return new AuthenticationState(user);
            }
        }
        catch
        {
            // Ignorar errores y devolver usuario no autenticado
        }

        return new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity()));
    }

    public void NotifyAuthStateChanged() =>
        NotifyAuthenticationStateChanged(GetAuthenticationStateAsync());

    private record AuthInfo(bool IsAuthenticated, string? Email, string[] Roles);
}


