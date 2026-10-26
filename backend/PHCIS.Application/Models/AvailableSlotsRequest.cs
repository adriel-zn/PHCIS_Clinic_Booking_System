namespace PHCIS.Application.Models;

/// <summary>Searches slot start instants in the half-open interval [From, To).</summary>
public sealed record AvailableSlotsRequest(
    Guid ClinicId,
    DateTimeOffset From,
    DateTimeOffset To,
    int Offset = 0,
    int Limit = 50);

