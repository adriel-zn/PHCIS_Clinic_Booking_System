namespace PHCIS.Application.Models;

/// <summary>Input for scheduling an existing patient into an existing time slot.</summary>
public sealed record BookAppointmentRequest(Guid PatientId, Guid TimeSlotId, string ReasonForVisit = "General consultation");

