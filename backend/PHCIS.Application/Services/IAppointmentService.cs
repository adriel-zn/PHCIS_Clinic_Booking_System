using PHCIS.Application.Models;

namespace PHCIS.Application.Services;

/// <summary>Orchestrates validated booking and patient-scoped cancellation use cases.</summary>
public interface IAppointmentService
{
    Task<AppointmentDto> BookAsync(BookAppointmentRequest request, CancellationToken cancellationToken = default);
    Task<AppointmentDto> CancelAsync(CancelAppointmentRequest request, CancellationToken cancellationToken = default);
    Task<AppointmentDto> RescheduleAsync(RescheduleAppointmentRequest request, CancellationToken cancellationToken = default);
}

