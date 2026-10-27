using System.Net.Http.Json;
using PHCIS.Web.Models;

namespace PHCIS.Web.Services;

/// <summary>Local Identity account operations; HttpOnly session and CSRF handling are provided by the HTTP pipeline.</summary>
public sealed class AccountClient(HttpClient http, LocalAuthenticationStateProvider authentication)
{
    public async Task LoginAsync(LoginModel model)
    {
        try
        {
            await SendAsync("api/account/login", new { model.Email, model.Password, model.RememberMe });
        }
        catch (AppointmentApiException error) when (error.StatusCode == 401)
        {
            throw new AppointmentApiException(401, "Email or password is incorrect.", error.TraceId);
        }
        await authentication.RefreshAsync();
    }

    public Task RegisterAsync(RegisterModel model) => SendAsync("api/account/register", new
    {
        model.FirstName,
        model.LastName,
        model.Email,
        model.PhoneNumber,
        model.DateOfBirth,
        model.Password,
        model.NationalId
    });

    public async Task LogoutAsync()
    {
        await SendAsync("api/account/logout", new { });
        await authentication.RefreshAsync();
    }

    public async Task UpdateProfileAsync(ProfileModel model)
    {
        await SendAsync("api/account/profile", new
        {
            model.FirstName,
            model.LastName,
            model.PhoneNumber,
            model.DateOfBirth,
            model.NationalId
        });
        await authentication.RefreshAsync();
    }

    private async Task SendAsync(string path, object body)
    {
        try
        {
            using var response = await http.PostAsJsonAsync(path, body);
            if (!response.IsSuccessStatusCode)
            {
                throw await AppointmentApiClient.ReadErrorAsync(response, default);
            }
        }
        catch (HttpRequestException)
        {
            throw new AppointmentApiException(0, "The service could not be reached. Please try again.");
        }
        catch (TaskCanceledException)
        {
            throw new AppointmentApiException(0, "The request timed out. Refresh the page before trying again.");
        }
    }
}

