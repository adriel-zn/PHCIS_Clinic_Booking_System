namespace PHCIS.Application.Models;

/// <summary>
/// PatientId must come from a trusted caller's identity context when exposed through an API.
/// It prevents cancelling another patient's appointment but does not authenticate the caller.
/// </summary>
public sealed record CancelAppointmentRequest(Guid AppointmentId, Guid PatientId, string Reason);

