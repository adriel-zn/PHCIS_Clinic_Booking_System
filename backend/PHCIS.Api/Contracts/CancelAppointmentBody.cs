using System.ComponentModel.DataAnnotations;

namespace PHCIS.Api.Contracts;

/// <summary>The reason is trimmed and validated by Application and Domain.</summary>
public sealed record CancelAppointmentBody([Required] string Reason);

