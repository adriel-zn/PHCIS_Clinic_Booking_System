using Microsoft.EntityFrameworkCore;
using PHCIS.Application.Abstractions;
using PHCIS.Domain.Entities;

namespace PHCIS.Infrastructure.Persistence.Repositories;

/// <summary>Pushes availability filtering and stable paging into the database.</summary>
public sealed class TimeSlotRepository(PhcisDbContext context) : ITimeSlotRepository
{
    public Task<TimeSlot?> FindAsync(Guid timeSlotId, CancellationToken cancellationToken)
        => context.TimeSlots.SingleOrDefaultAsync(x => x.Id == timeSlotId, cancellationToken);

    public Task<bool> ClinicExistsAsync(Guid clinicId, CancellationToken cancellationToken)
        => context.Clinics.AnyAsync(x => x.Id == clinicId, cancellationToken);

    public async Task<IReadOnlyList<TimeSlot>> FindAvailableAsync(
        Guid clinicId,
        DateTimeOffset from,
        DateTimeOffset to,
        DateTimeOffset now,
        int offset,
        int take,
        CancellationToken cancellationToken)
    {
        return await context.TimeSlots
            .AsNoTracking()
            .Where(slot => slot.ClinicId == clinicId && !slot.IsBlocked &&
                slot.StartsAt >= from && slot.StartsAt < to && slot.StartsAt > now &&
                !context.Appointments.Any(appointment =>
                    appointment.TimeSlotId == slot.Id &&
                    appointment.Status != AppointmentStatus.Cancelled))
            .OrderBy(slot => slot.StartsAt)
            .ThenBy(slot => slot.Id)
            .Skip(offset)
            .Take(take)
            .ToListAsync(cancellationToken);
    }
}

