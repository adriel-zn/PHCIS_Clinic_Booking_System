using PHCIS.Domain.Entities;

namespace PHCIS.Application.Abstractions;

/// <summary>Reads clinic schedules; query results must not depend on loaded navigation collections.</summary>
public interface ITimeSlotRepository
{
    Task<TimeSlot?> FindAsync(Guid timeSlotId, CancellationToken cancellationToken);
    Task<bool> ClinicExistsAsync(Guid clinicId, CancellationToken cancellationToken);

    /// <summary>
    /// Returns slots starting in [from, to), strictly after now, without non-cancelled appointments.
    /// Orders by StartsAt then Id before applying offset and take.
    /// </summary>
    Task<IReadOnlyList<TimeSlot>> FindAvailableAsync(
        Guid clinicId,
        DateTimeOffset from,
        DateTimeOffset to,
        DateTimeOffset now,
        int offset,
        int take,
        CancellationToken cancellationToken);
}

