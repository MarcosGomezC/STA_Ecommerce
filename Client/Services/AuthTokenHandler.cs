using Microsoft.JSInterop;
using System.Net.Http.Headers;

namespace STA_Ecommerce.Client.Services;

public class AuthTokenHandler : DelegatingHandler
{
    private readonly IJSRuntime _jsRuntime;
    private const string TokenKey = "sta_auth_token";

    public AuthTokenHandler(IJSRuntime jsRuntime)
    {
        _jsRuntime = jsRuntime;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        try
        {
            var token = await _jsRuntime.InvokeAsync<string>("localStorage.getItem", TokenKey);
            if (!string.IsNullOrWhiteSpace(token))
            {
                // Remover comillas si las hay (a veces localStorage las agrega)
                token = token.Trim('"', '\'');
                if (!string.IsNullOrWhiteSpace(token))
                {
                    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
                }
            }
        }
        catch
        {
            // Si hay un error obteniendo el token, continuar sin él
            // El servidor responderá con 401 si es necesario
        }

        return await base.SendAsync(request, cancellationToken);
    }
}

