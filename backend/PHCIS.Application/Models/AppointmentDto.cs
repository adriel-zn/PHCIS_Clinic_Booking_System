namespace PHCIS.Application.Models;

public sealed record AppointmentDto(
    Guid Id,
    Guid PatientId,
    Guid TimeSlotId,
    PHCIS.Domain.Entities.AppointmentStatus Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset? CancelledAt,
    string? CancellationReason);
