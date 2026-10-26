using System.Net.Http.Json;
using System.Text.Json;
using PHCIS.Web.Models;

namespace PHCIS.Web.Services;

/// <summary>Read-only API contracts for the clinic directory and patient-owned appointment list.</summary>
public sealed class PortalClient(HttpClient http)
{
    public Task<ClinicManagementAccess> ClinicManagementAccessAsync() =>
        ReadAsync<ClinicManagementAccess>("api/clinic-management/access");

    public async Task CreateClinicAsync(CreateClinicModel model)
    {
        try
        {
            using var response = await http.PostAsJsonAsync("api/clinic-management", model);
            if (response.StatusCode == System.Net.HttpStatusCode.Forbidden)
                throw new AppointmentApiException(403, "Clinic administrator access is required to add a clinic.");
            if (!response.IsSuccessStatusCode)
                throw await AppointmentApiClient.ReadErrorAsync(response, default);
        }
        catch (HttpRequestException)
        {
            throw new AppointmentApiException(0, "The service could not be reached. Check the clinic list before trying again.");
        }
        catch (TaskCanceledException)
        {
            throw new AppointmentApiException(0, "The request timed out. Check the clinic list before trying again.");
        }
    }

    public Task<SlotSummary> SlotAsync(Guid clinicId, Guid slotId) =>
        ReadAsync<SlotSummary>($"api/clinics/{clinicId}/slots/{slotId}");
    public async Task<IReadOnlyList<ClinicCard>> ClinicsAsync() => await ReadAsync<List<ClinicCard>>("api/clinics");
    public async Task<IReadOnlyList<AppointmentDetails>> AppointmentsAsync() => await ReadAsync<List<AppointmentDetails>>("api/appointments");

    private async Task<T> ReadAsync<T>(string path) where T : class
    {
        try
        {
            using var response = await http.GetAsync(path);
            if (!response.IsSuccessStatusCode)
            {
                throw await AppointmentApiClient.ReadErrorAsync(response, default);
            }
            return await response.Content.ReadFromJsonAsync<T>() ?? throw new JsonException("Empty API response.");
        }
        catch (HttpRequestException)
        {
            throw new AppointmentApiException(0, "The service could not be reached. Please try again.");
        }
        catch (TaskCanceledException)
        {
            throw new AppointmentApiException(0, "The service took too long to respond. Please try again.");
        }
        catch (JsonException)
        {
            throw new AppointmentApiException(0, "The service returned an unreadable response. Please try again later.");
        }
    }
}

