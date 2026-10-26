using System.ComponentModel.DataAnnotations;

namespace PHCIS.Api.Contracts;

/// <summary>Patient identity is supplied by the validated token, never by the request body.</summary>
public sealed record BookAppointmentBody([Required] Guid TimeSlotId, string ReasonForVisit = "General consultation");

