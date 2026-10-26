namespace PHCIS.Application.Services;

/// <summary>Creates clinics with an initial fortnight of weekday availability.</summary>
public interface IClinicManagementService
{
    Task<Guid> CreateAsync(CreateClinicRequest request, CancellationToken cancellationToken);
    Task UpdateAsync(Guid clinicId, CreateClinicRequest request, CancellationToken cancellationToken);
    Task<IReadOnlyList<ManagedTimeSlot>> SlotsAsync(Guid clinicId, DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken);
    Task<Guid> AddSlotAsync(Guid clinicId, SlotScheduleRequest request, CancellationToken cancellationToken);
    Task UpdateSlotAsync(Guid clinicId, Guid slotId, SlotScheduleRequest request, CancellationToken cancellationToken);
    Task SetBlockedAsync(Guid clinicId, Guid slotId, bool blocked, CancellationToken cancellationToken);
}

/// <summary>Clinic details supplied by an authorized clinic administrator.</summary>
public sealed record CreateClinicRequest(string Name, string Address, string? PhoneNumber);

/// <summary>Absolute timestamps with an explicit offset, validated before persistence.</summary>
public sealed record SlotScheduleRequest(DateTimeOffset StartsAt, DateTimeOffset EndsAt);
public sealed record ManagedTimeSlot(Guid Id, DateTimeOffset StartsAt, DateTimeOffset EndsAt,
    bool IsBlocked, bool IsBooked, bool HasAppointments);
