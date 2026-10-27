using PHCIS.Domain.Entities;
namespace PHCIS.Application.Abstractions;

/// <summary>Stages durable notifications before commit and broadcasts only after commit.</summary>
public interface IAppointmentEffects
{
    void Stage(Appointment appointment);
    Task CommittedAsync(Appointment appointment, CancellationToken cancellationToken);
}
