using PHCIS.Application.Models;

namespace PHCIS.Application.Abstractions;

/// <summary>Read models for the patient dashboard, clinic directory, and appointment history.</summary>
public interface IPortalReader
{
    Task<IReadOnlyList<ClinicCard>> ClinicsAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<AppointmentDetails>> AppointmentsAsync(Guid patientId, CancellationToken cancellationToken);
    Task<PatientProfile?> ProfileAsync(Guid patientId, CancellationToken cancellationToken);
    Task<TimeSlotDto?> SlotAsync(Guid clinicId, Guid slotId, CancellationToken cancellationToken);
}

