using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;
using PHCIS.Web.Models;

namespace PHCIS.Web.Services;

/// <summary>Rehydrates session identity from the API. Browser storage never holds credentials or bearer tokens.</summary>
public sealed class LocalAuthenticationStateProvider(HttpClient http) : AuthenticationStateProvider
{
    private Task<AuthenticationState>? _state;
    public PatientProfile? Profile { get; private set; }

    public override Task<AuthenticationState> GetAuthenticationStateAsync() => _state ??= LoadAsync();

    public async Task RefreshAsync()
    {
        _state = LoadAsync();
        NotifyAuthenticationStateChanged(_state);
        await _state;
    }

    private async Task<AuthenticationState> LoadAsync()
    {
        Profile = null;
        try
        {
            using var response = await http.GetAsync("api/account/me");
            if (response.StatusCode == HttpStatusCode.OK)
            {
                Profile = await response.Content.ReadFromJsonAsync<PatientProfile>();
            }
        }
        catch (HttpRequestException) { }
        catch (TaskCanceledException) { }
        if (Profile is null)
        {
            return new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity()));
        }
        return new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim(ClaimTypes.Name, Profile.FullName),
            new Claim(ClaimTypes.Email, Profile.Email ?? ""),
            new Claim("patient_id", Profile.Id.ToString())
        ], "IdentityCookie")));
    }
}

