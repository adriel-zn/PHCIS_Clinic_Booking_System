namespace PHCIS.Application.Models;

public sealed record TimeSlotDto(
    Guid Id,
    Guid ClinicId,
    DateTimeOffset StartsAt,
    DateTimeOffset EndsAt);
