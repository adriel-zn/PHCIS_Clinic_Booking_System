using PHCIS.Domain.Entities;

namespace PHCIS.Application.Abstractions;

/// <summary>Persists a clinic and its initial slots as one operation.</summary>
public interface IClinicWriter
{
    Task CreateAsync(Clinic clinic, IReadOnlyCollection<TimeSlot> slots, CancellationToken cancellationToken);
    Task UpdateAsync(Guid clinicId, string name, string address, string? phoneNumber, CancellationToken cancellationToken);
    Task<IReadOnlyList<PHCIS.Application.Services.ManagedTimeSlot>> SlotsAsync(Guid clinicId, DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken);
    Task<Guid> SaveSlotAsync(Guid clinicId, Guid? slotId, DateTimeOffset start, DateTimeOffset end, DateTimeOffset now, CancellationToken cancellationToken);
    Task SetBlockedAsync(Guid clinicId, Guid slotId, bool blocked, DateTimeOffset now, CancellationToken cancellationToken);
}
