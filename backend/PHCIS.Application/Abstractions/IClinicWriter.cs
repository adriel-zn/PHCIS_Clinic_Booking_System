using PHCIS.Domain.Entities;

namespace PHCIS.Application.Abstractions;

/// <summary>Persists a clinic and its initial slots as one operation.</summary>
public interface IClinicWriter
{
    Task CreateAsync(Clinic clinic, IReadOnlyCollection<TimeSlot> slots, CancellationToken cancellationToken);
}
