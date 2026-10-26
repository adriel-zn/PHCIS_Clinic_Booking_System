using PHCIS.Domain.Entities;

namespace PHCIS.Application.Abstractions;

/// <summary>Tracks appointment changes in the same scope as IUnitOfWork.</summary>
public interface IAppointmentRepository
{
    /// <summary>Loads a tracked appointment and its slot for domain lifecycle operations.</summary>
    Task<Appointment?> FindWithSlotAsync(Guid appointmentId, CancellationToken cancellationToken);

    /// <summary>All statuses except Cancelled occupy a slot, including historical terminal states.</summary>
    Task<bool> IsSlotOccupiedAsync(Guid timeSlotId, CancellationToken cancellationToken);

    void Add(Appointment appointment);
}

