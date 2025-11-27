using Microsoft.JSInterop;

namespace STA_Ecommerce.Client.Services;

public class WishlistService
{
    private const string StorageKey = "sta_wishlist";
    private readonly IJSRuntime _js;

    public WishlistService(IJSRuntime js)
    {
        _js = js;
    }

    public async Task<HashSet<int>> GetWishlistAsync()
    {
        var json = await _js.InvokeAsync<string?>("localStorage.getItem", StorageKey);
        if (string.IsNullOrWhiteSpace(json))
        {
            return new HashSet<int>();
        }

        try
        {
            var ids = System.Text.Json.JsonSerializer.Deserialize<HashSet<int>>(json);
            return ids ?? new HashSet<int>();
        }
        catch
        {
            return new HashSet<int>();
        }
    }

    public async Task ToggleAsync(int productId)
    {
        var set = await GetWishlistAsync();
        if (set.Contains(productId))
        {
            set.Remove(productId);
        }
        else
        {
            set.Add(productId);
        }

        var json = System.Text.Json.JsonSerializer.Serialize(set);
        await _js.InvokeVoidAsync("localStorage.setItem", StorageKey, json);
    }

    public async Task<bool> IsFavoriteAsync(int productId)
    {
        var set = await GetWishlistAsync();
        return set.Contains(productId);
    }
}


