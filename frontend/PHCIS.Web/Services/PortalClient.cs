using System.Net.Http.Json;
using System.Text.Json;
using PHCIS.Web.Models;

namespace PHCIS.Web.Services;

/// <summary>Read-only API contracts for the clinic directory and patient-owned appointment list.</summary>
public sealed class PortalClient(HttpClient http)
{
    public Task<ClinicManagementAccess> ClinicManagementAccessAsync() =>
        ReadAsync<ClinicManagementAccess>("api/clinic-management/access");

    public Task CreateClinicAsync(CreateClinicModel model) => WriteAsync("api/clinic-management", model);
    public Task UpdateClinicAsync(Guid id, CreateClinicModel model) => WriteAsync($"api/clinic-management/{id}/details", model);
    public Task GrantAdministratorAsync(GrantAdministratorModel model) => WriteAsync("api/administrators", model);
    public async Task<IReadOnlyList<AdministratorDetails>> AdministratorsAsync() =>
        await ReadAsync<List<AdministratorDetails>>("api/administrators");
    public Task RemoveAdministratorAsync(string id) =>
        WriteAsync($"api/administrators/{Uri.EscapeDataString(id)}/removal", new { ConfirmRemoval = true });
    public async Task<IReadOnlyList<StaffDetails>> StaffAsync() => await ReadAsync<List<StaffDetails>>("api/staff");
    public Task AssignStaffAsync(AssignStaffModel model) => WriteAsync("api/staff", model);
    public Task RemoveStaffAsync(string id) =>
        WriteAsync($"api/staff/{Uri.EscapeDataString(id)}/removal", new { ConfirmRemoval = true });
    public Task<StaffDashboardAccess> StaffDashboardAccessAsync() => ReadAsync<StaffDashboardAccess>("api/staff-appointments/access");
    public Task<StaffAppointmentsPage> ClinicAppointmentsAsync(Guid clinicId, DateOnly date, string? status, int offset) =>
        ReadAsync<StaffAppointmentsPage>($"api/staff-appointments?clinicId={clinicId}&date={date:yyyy-MM-dd}&offset={offset}&limit=50" +
            (string.IsNullOrEmpty(status) ? "" : $"&status={Uri.EscapeDataString(status)}"));
    public Task FinalizeAppointmentAsync(Guid clinicId, Guid appointmentId, bool completed) =>
        WriteAsync($"api/staff-appointments/{clinicId}/{appointmentId}/" + (completed ? "completion" : "no-show"),
            new { ConfirmStatusChange = true });
    public async Task<IReadOnlyList<ManagedTimeSlot>> ManagedSlotsAsync(Guid id, DateOnly date)
    {
        var from = new DateTimeOffset(date.ToDateTime(TimeOnly.MinValue), TimeSpan.FromHours(2));
        var to = from.AddDays(1);
        return await ReadAsync<List<ManagedTimeSlot>>($"api/clinic-management/{id}/slots?from={Uri.EscapeDataString(from.ToString("O"))}&to={Uri.EscapeDataString(to.ToString("O"))}");
    }
    public Task SaveSlotAsync(Guid clinicId, Guid? slotId, SlotScheduleModel model)
    {
        if (model.StartsAt is null || model.EndsAt is null || model.EndsAt <= model.StartsAt)
            throw new AppointmentApiException(400, "Enter a start and end time, with the end after the start.");
        var start = new DateTimeOffset(DateTime.SpecifyKind(model.StartsAt.Value, DateTimeKind.Unspecified), TimeSpan.FromHours(2));
        var end = new DateTimeOffset(DateTime.SpecifyKind(model.EndsAt.Value, DateTimeKind.Unspecified), TimeSpan.FromHours(2));
        var path = $"api/clinic-management/{clinicId}/slots" + (slotId is null ? "" : $"/{slotId}/schedule");
        return WriteAsync(path, new { StartsAt = start, EndsAt = end });
    }
    public Task SetSlotBlockedAsync(Guid clinicId, Guid slotId, bool blocked) =>
        WriteAsync($"api/clinic-management/{clinicId}/slots/{slotId}/availability", new { IsBlocked = blocked });

    private async Task WriteAsync(string path, object model)
    {
        try
        {
            using var response = await http.PostAsJsonAsync(path, model);
            if (response.StatusCode == System.Net.HttpStatusCode.Forbidden)
                throw new AppointmentApiException(403, path.StartsWith("api/staff-appointments/", StringComparison.Ordinal)
                    ? "The Staff role is required for this action. Ask an administrator to check your access."
                    : "Clinic administrator access is required for this action.");
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
            if (response.StatusCode == System.Net.HttpStatusCode.Forbidden && path.StartsWith("api/staff-appointments", StringComparison.Ordinal))
                throw new AppointmentApiException(403, "The Staff role is required to view clinic appointments.");
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

