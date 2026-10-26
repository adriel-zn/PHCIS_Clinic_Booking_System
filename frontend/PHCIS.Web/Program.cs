using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using PHCIS.Web;
using PHCIS.Web.Configuration;
using PHCIS.Web.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

var options = builder.Configuration.GetSection("Api").Get<PortalOptions>() ?? new PortalOptions();
if (!Uri.TryCreate(options.BaseUrl, UriKind.Absolute, out var apiUri) ||
    (apiUri.Scheme != Uri.UriSchemeHttps && !(apiUri.IsLoopback && apiUri.Scheme == Uri.UriSchemeHttp)) ||
    !string.IsNullOrEmpty(apiUri.Query) || !string.IsNullOrEmpty(apiUri.Fragment) ||
    !string.IsNullOrEmpty(apiUri.UserInfo) ||
    options.ClinicUtcOffsetMinutes is < -840 or > 840)
{
    throw new InvalidOperationException("Configure a valid API URL and clinic UTC offset.");
}
options.BaseUrl = apiUri.AbsoluteUri.TrimEnd('/') + "/";

builder.Services.AddSingleton(options);
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddAuthorizationCore();
builder.Services.AddTransient<CookieSessionHandler>();
builder.Services.AddHttpClient("Api", client =>
{
    client.BaseAddress = new Uri(options.BaseUrl);
    client.Timeout = TimeSpan.FromSeconds(30);
}).AddHttpMessageHandler<CookieSessionHandler>();
builder.Services.AddScoped<IAppointmentApiClient>(services => new AppointmentApiClient(
    services.GetRequiredService<IHttpClientFactory>().CreateClient("Api"), options));
builder.Services.AddScoped<LocalAuthenticationStateProvider>(services => new LocalAuthenticationStateProvider(
    services.GetRequiredService<IHttpClientFactory>().CreateClient("Api")));
builder.Services.AddScoped<Microsoft.AspNetCore.Components.Authorization.AuthenticationStateProvider>(services =>
    services.GetRequiredService<LocalAuthenticationStateProvider>());
builder.Services.AddScoped<AccountClient>(services => new AccountClient(
    services.GetRequiredService<IHttpClientFactory>().CreateClient("Api"), services.GetRequiredService<LocalAuthenticationStateProvider>()));
builder.Services.AddScoped<PortalClient>(services => new PortalClient(
    services.GetRequiredService<IHttpClientFactory>().CreateClient("Api")));
await builder.Build().RunAsync();

