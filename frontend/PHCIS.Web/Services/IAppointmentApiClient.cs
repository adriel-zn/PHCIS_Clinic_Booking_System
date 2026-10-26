using PHCIS.Web.Models;

namespace PHCIS.Web.Services;

/// <summary>Single HTTP boundary used by components; authentication is handled by the message handler.</summary>
public interface IAppointmentApiClient
{
    Task<AvailableSlotsPage> FindAvailableAsync(AvailabilityQuery query, CancellationToken cancellationToken);
    Task<AppointmentSummary> BookAsync(Guid slotId, CancellationToken cancellationToken, string reasonForVisit = "General consultation");
    Task<AppointmentSummary> CancelAsync(Guid appointmentId, string reason, CancellationToken cancellationToken);
    Task<AppointmentSummary> RescheduleAsync(Guid appointmentId, Guid slotId, CancellationToken cancellationToken);
}

