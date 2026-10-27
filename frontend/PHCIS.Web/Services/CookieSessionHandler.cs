using System.Net.Http.Json;
using Microsoft.AspNetCore.Components.WebAssembly.Http;
using PHCIS.Web.Configuration;

namespace PHCIS.Web.Services;

/// <summary>Includes HttpOnly cookies and obtains a fresh antiforgery token for every unsafe API request.</summary>
public sealed class CookieSessionHandler(PortalOptions options) : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (request.RequestUri is null || !new Uri(options.BaseUrl).IsBaseOf(request.RequestUri))
        {
            return await base.SendAsync(request, cancellationToken);
        }
        request.SetBrowserRequestCredentials(BrowserRequestCredentials.Include);
        if (request.Method != HttpMethod.Get && request.Method != HttpMethod.Head && request.Method != HttpMethod.Options)
        {
            using var csrfRequest = new HttpRequestMessage(HttpMethod.Get, new Uri(new Uri(options.BaseUrl), "api/account/csrf"));
            csrfRequest.SetBrowserRequestCredentials(BrowserRequestCredentials.Include);
            using var csrf = await base.SendAsync(csrfRequest, cancellationToken);
            csrf.EnsureSuccessStatusCode();
            var token = await csrf.Content.ReadFromJsonAsync<CsrfResponse>(cancellationToken)
                ?? throw new HttpRequestException("The session could not be verified.");
            request.Headers.Add("X-CSRF-TOKEN", token.RequestToken);
        }
        return await base.SendAsync(request, cancellationToken);
    }

    private sealed record CsrfResponse(string RequestToken);
}

