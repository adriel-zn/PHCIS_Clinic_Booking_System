using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.AspNetCore.Components.WebAssembly.Http;
using PHCIS.Web.Configuration;
namespace PHCIS.Web.Services;

/// <summary>Authenticated refresh signals; patient data is reloaded only through authorized HTTP endpoints.</summary>
public sealed class RealtimeUpdates(PortalOptions options, PortalClient portal) : IAsyncDisposable
{
    private HubConnection? _connection;
    public async Task StartAsync()
    {
        if (_connection is not null)
            return;
        _connection = new HubConnectionBuilder().WithUrl(options.BaseUrl.TrimEnd('/') + "/hubs/appointments", transport =>
        {
            transport.Transports = HttpTransportType.LongPolling;
            transport.HttpMessageHandlerFactory = inner => new CredentialsHandler(inner);
        }).WithAutomaticReconnect().Build();
        _connection.On("AppointmentsChanged", portal.NotifyChangedAsync);
        try
        { await _connection.StartAsync(); }
        catch (Exception) { await _connection.DisposeAsync(); _connection = null; }
    }
    public async ValueTask DisposeAsync() { if (_connection is not null) await _connection.DisposeAsync(); }
    private sealed class CredentialsHandler(HttpMessageHandler inner) : DelegatingHandler(inner)
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            request.SetBrowserRequestCredentials(BrowserRequestCredentials.Include);
            return base.SendAsync(request, cancellationToken);
        }
    }
}
