namespace PHCIS.Application.Models;

public sealed record RescheduleAppointmentRequest(Guid AppointmentId, Guid PatientId, Guid TimeSlotId);

