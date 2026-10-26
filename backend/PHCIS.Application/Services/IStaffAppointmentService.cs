using PHCIS.Application.Models;

namespace PHCIS.Application.Services;

public interface IStaffAppointmentService
{
    Task<StaffAppointmentsPage> ListAsync(StaffAppointmentsRequest request, CancellationToken cancellationToken);
    Task CompleteAsync(Guid clinicId, Guid appointmentId, CancellationToken cancellationToken);
    Task MarkNoShowAsync(Guid clinicId, Guid appointmentId, CancellationToken cancellationToken);
}
