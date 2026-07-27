using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Solace.WebPortal.Common;

var builder = WebAssemblyHostBuilder.CreateDefault(args);

builder.Services.AddAuthorizationCore(options =>
{
    options.AddPermissionPolicies();
});
builder.Services.AddCascadingAuthenticationState();
builder.Services.AddAuthenticationStateDeserialization();

builder.Services.AddScoped(sp => new HttpClient
{
    BaseAddress = new Uri(builder.HostEnvironment.BaseAddress)
});

builder.Services.AddMemoryCache();

builder.Services.AddScoped<Solace.WebPortal.Client.Features.Catalog.CatalogCacheService>();

await builder.Build().RunAsync();
