using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using STA_Ecommerce.Client;
using STA_Ecommerce.Client.Auth;
using STA_Ecommerce.Client.Services;
using Microsoft.AspNetCore.Components.Authorization;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// Configurar HttpClient con handler para agregar token JWT automáticamente
builder.Services.AddScoped<AuthTokenHandler>();
builder.Services.AddScoped(sp =>
{
    var handler = sp.GetRequiredService<AuthTokenHandler>();
    handler.InnerHandler = new HttpClientHandler();
    return new HttpClient(handler)
    {
        BaseAddress = new Uri(builder.HostEnvironment.BaseAddress)
    };
});
builder.Services.AddScoped<ProductApiClient>();
builder.Services.AddScoped<AdminProductApiClient>();
builder.Services.AddScoped<WishlistService>();
builder.Services.AddScoped<AuthApiClient>();
builder.Services.AddScoped<TokenDebugService>();
builder.Services.AddScoped<AuthenticationStateProvider, SimpleAuthStateProvider>();
builder.Services.AddAuthorizationCore();

await builder.Build().RunAsync();
