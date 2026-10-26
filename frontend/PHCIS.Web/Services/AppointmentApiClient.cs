using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using PHCIS.Web.Configuration;
using PHCIS.Web.Models;

namespace PHCIS.Web.Services;

/// <summary>Encodes the API contract and translates failures. Writes are never retried automatically.</summary>
public sealed class AppointmentApiClient(HttpClient http, PortalOptions options) : IAppointmentApiClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public Task<AvailableSlotsPage> FindAvailableAsync(AvailabilityQuery query, CancellationToken cancellationToken)
    {
        var from = new DateTimeOffset(query.Date.ToDateTime(TimeOnly.MinValue), options.ClinicOffset);
        var to = from.AddDays(1);
        var path = $"api/clinics/{query.ClinicId:D}/available-slots" +
            $"?from={Uri.EscapeDataString(from.ToString("O", CultureInfo.InvariantCulture))}" +
            $"&to={Uri.EscapeDataString(to.ToString("O", CultureInfo.InvariantCulture))}" +
            $"&offset={query.Offset}&limit={query.Limit}";
        return SendAsync<AvailableSlotsPage>(new HttpRequestMessage(HttpMethod.Get, path), false, cancellationToken);
    }

    public Task<AppointmentSummary> BookAsync(Guid slotId, CancellationToken cancellationToken, string reasonForVisit = "General consultation") =>
        SendAsync<AppointmentSummary>(new HttpRequestMessage(HttpMethod.Post, "api/appointments")
        {
            Content = JsonContent.Create(new { timeSlotId = slotId, reasonForVisit })
        }, true, cancellationToken);

    public Task<AppointmentSummary> CancelAsync(Guid appointmentId, string reason, CancellationToken cancellationToken) =>
        SendAsync<AppointmentSummary>(new HttpRequestMessage(HttpMethod.Post, $"api/appointments/{appointmentId:D}/cancellation")
        {
            Content = JsonContent.Create(new { reason = reason.Trim() })
        }, true, cancellationToken);

    public Task<AppointmentSummary> RescheduleAsync(Guid appointmentId, Guid slotId, CancellationToken cancellationToken) =>
        SendAsync<AppointmentSummary>(new HttpRequestMessage(HttpMethod.Post, $"api/appointments/{appointmentId:D}/reschedule")
        {
            Content = JsonContent.Create(new { timeSlotId = slotId })
        }, true, cancellationToken);

    private async Task<T> SendAsync<T>(HttpRequestMessage request, bool isWrite, CancellationToken cancellationToken)
    {
        using (request)
        {
            try
            {
                using var response = await http.SendAsync(request, cancellationToken);
                if (!response.IsSuccessStatusCode)
                {
                    throw await ReadErrorAsync(response, cancellationToken);
                }

                return await response.Content.ReadFromJsonAsync<T>(JsonOptions, cancellationToken)
                    ?? throw new JsonException("Empty API response.");
            }
            catch (HttpRequestException)
            {
                throw new AppointmentApiException(0, isWrite
                    ? "We could not confirm the result. Contact your clinic before trying this request again."
                    : "We could not reach the clinic service. Check your connection and try again.");
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                throw new AppointmentApiException(0, isWrite
                    ? "The request timed out and its result is uncertain. Contact your clinic before trying again."
                    : "The clinic service took too long to respond. Please try again.");
            }
            catch (JsonException)
            {
                throw new AppointmentApiException(0, isWrite
                    ? "The response could not be read. Your request may have succeeded; contact your clinic before trying again."
                    : "The clinic service returned an unreadable response. Please try again later.");
            }
        }
    }

    internal static async Task<AppointmentApiException> ReadErrorAsync(
        HttpResponseMessage response, CancellationToken cancellationToken)
    {
        string? traceId = null;
        string? detail = null;
        var errors = new Dictionary<string, string[]>();

        try
        {
            var body = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions, cancellationToken);
            if (body.ValueKind == JsonValueKind.Object)
            {
                if (body.TryGetProperty("traceId", out var trace) && trace.ValueKind == JsonValueKind.String)
                {
                    traceId = trace.GetString();
                }
                if (body.TryGetProperty("detail", out var message) && message.ValueKind == JsonValueKind.String)
                {
                    detail = message.GetString();
                }
                if (body.TryGetProperty("errors", out var fields) && fields.ValueKind == JsonValueKind.Object)
                {
                    foreach (var field in fields.EnumerateObject())
                    {
                        if (field.Value.ValueKind == JsonValueKind.Array)
                        {
                            errors[field.Name] = field.Value.EnumerateArray()
                                .Where(value => value.ValueKind == JsonValueKind.String)
                                .Select(value => value.GetString()!).ToArray();
                        }
                    }
                }
            }
        }
        catch (JsonException)
        {
            // Proxies can return HTML or empty bodies; never display the raw response.
        }

        var status = (int)response.StatusCode;
        var safeMessage = status switch
        {
            400 => "Please check the details and try again.",
            401 => "Your session has expired. Sign in again to continue.",
            403 => "Your account is not linked to a patient profile. Contact your clinic.",
            404 => detail ?? "The clinic, slot, or appointment could not be found for your account.",
            409 => detail ?? "This request conflicts with the current appointment state. Refresh availability.",
            _ => "The clinic service is temporarily unavailable. Please try again later."
        };
        return new AppointmentApiException(status, safeMessage, traceId, status == 400 ? errors : null);
    }
}

