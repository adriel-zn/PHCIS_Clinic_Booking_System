namespace PHCIS.Application.Models;

/// <summary>A bounded page ordered by start time and slot ID; availability is advisory until booking saves.</summary>
public sealed record AvailableSlotsDto(
    IReadOnlyList<TimeSlotDto> Items,
    int Offset,
    int Limit,
    bool HasMore);

