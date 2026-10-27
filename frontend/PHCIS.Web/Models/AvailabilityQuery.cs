namespace PHCIS.Web.Models;

/// <summary>A clinic-local calendar day, converted to an explicit UTC interval by the API client.</summary>
public sealed record AvailabilityQuery(Guid ClinicId, DateOnly Date, int Offset = 0, int Limit = 12);

