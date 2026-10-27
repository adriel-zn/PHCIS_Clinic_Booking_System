using Microsoft.EntityFrameworkCore;
using PHCIS.Application.Abstractions;
using PHCIS.Domain.Entities;

namespace PHCIS.Infrastructure.Persistence.Repositories;

/// <summary>Loads lifecycle dependencies explicitly and stages writes without committing.</summary>
public sealed class AppointmentRepository(PhcisDbContext context) : IAppointmentRepository
{
    public Task<Appointment?> FindWithSlotAsync(Guid appointmentId, CancellationToken cancellationToken)
        => context.Appointments.Include(x => x.TimeSlot)
            .SingleOrDefaultAsync(x => x.Id == appointmentId, cancellationToken);

    public Task<bool> IsSlotOccupiedAsync(Guid timeSlotId, CancellationToken cancellationToken)
        => context.Appointments.AnyAsync(
            x => x.TimeSlotId == timeSlotId && x.Status != AppointmentStatus.Cancelled,
            cancellationToken);

    public void Add(Appointment appointment) => context.Appointments.Add(appointment);
}

