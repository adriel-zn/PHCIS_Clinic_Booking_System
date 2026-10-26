namespace PHCIS.Web.Models;

/// <summary>HTTP response contract, deliberately independent of backend assemblies.</summary>
public sealed record AppointmentSummary(
    Guid Id,
    Guid PatientId,
    Guid TimeSlotId,
    string Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset? CancelledAt,
    string? CancellationReason);

