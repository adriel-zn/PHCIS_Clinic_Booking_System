using PHCIS.Domain.Entities;

namespace PHCIS.Application.Abstractions;

/// <summary>Reads persisted patients without exposing an ORM to Application.</summary>
public interface IPatientReader
{
    Task<Patient?> FindAsync(Guid patientId, CancellationToken cancellationToken);
}

