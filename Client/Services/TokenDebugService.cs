using Microsoft.JSInterop;

namespace STA_Ecommerce.Client.Services;

public class TokenDebugService
{
    private readonly IJSRuntime _jsRuntime;
    private const string TokenKey = "sta_auth_token";

    public TokenDebugService(IJSRuntime jsRuntime)
    {
        _jsRuntime = jsRuntime;
    }

    public async Task<string?> GetTokenAsync()
    {
        try
        {
            return await _jsRuntime.InvokeAsync<string>("localStorage.getItem", TokenKey);
        }
        catch
        {
            return null;
        }
    }

    public async Task<bool> HasTokenAsync()
    {
        var token = await GetTokenAsync();
        return !string.IsNullOrWhiteSpace(token);
    }
}

