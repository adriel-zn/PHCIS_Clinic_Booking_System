using System.Net;
using System.Net.Http.Json;
using PHCIS.Web.Configuration;
using PHCIS.Web.Services;

namespace PHCIS.Web.Tests;

public sealed class CookieSessionHandlerTests
{
    [Theory]
    [InlineData("https://api.test/api/appointments", true)]
    [InlineData("https://other.test/api/appointments", false)]
    public async Task CredentialsAreIncludedOnlyForConfiguredApi(string address, bool expected)
    {
        bool included = false;
        using var http = new HttpClient(new CookieSessionHandler(new PortalOptions { BaseUrl = "https://api.test/" })
        {
            InnerHandler = new StubHandler(request =>
            {
                included = request.Options.Any(option => option.Value is IDictionary<string, object> values &&
                    values.TryGetValue("credentials", out var value) && value is string text && text == "include");
                Assert.Null(request.Headers.Authorization);
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
            })
        });
        using var response = await http.GetAsync(address);
        Assert.Equal(expected, included);
    }

    [Fact]
    public async Task UnsafeRequestObtainsFreshCsrfTokenBeforeSendingBody()
    {
        var paths = new List<string>();
        using var http = new HttpClient(new CookieSessionHandler(new PortalOptions { BaseUrl = "https://api.test/" })
        {
            InnerHandler = new StubHandler(request =>
            {
                paths.Add(request.RequestUri!.AbsolutePath);
                if (request.Method == HttpMethod.Get)
                {
                    return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = JsonContent.Create(new { requestToken = "test-csrf" })
                    });
                }
                Assert.Equal("test-csrf", Assert.Single(request.Headers.GetValues("X-CSRF-TOKEN")));
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.Created));
            })
        });
        using var response = await http.PostAsJsonAsync("https://api.test/api/appointments", new { timeSlotId = Guid.NewGuid() });
        Assert.Equal(new[] { "/api/account/csrf", "/api/appointments" }, paths);
    }
}

