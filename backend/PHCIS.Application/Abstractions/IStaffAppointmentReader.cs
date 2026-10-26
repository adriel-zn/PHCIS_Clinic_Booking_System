using PHCIS.Application.Models;

namespace PHCIS.Application.Abstractions;

/// <summary>Clinic/day appointment projections available only through the staff-authorized API.</summary>
public interface IStaffAppointmentReader
{
    Task<StaffAppointmentsPage> ReadAsync(StaffAppointmentsRequest request, DateTimeOffset now, CancellationToken cancellationToken);
}
