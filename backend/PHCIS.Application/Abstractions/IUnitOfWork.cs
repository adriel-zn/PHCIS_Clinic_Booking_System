namespace PHCIS.Application.Abstractions;

/// <summary>Commits the current scoped operation; persistence conflicts become AppointmentConflictException.</summary>
public interface IUnitOfWork
{
    Task SaveChangesAsync(CancellationToken cancellationToken);
}

