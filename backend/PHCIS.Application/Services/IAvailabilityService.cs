using PHCIS.Application.Models;

namespace PHCIS.Application.Services;

/// <summary>Queries future slot availability without changing persisted state.</summary>
public interface IAvailabilityService
{
    Task<AvailableSlotsDto> GetAvailableSlotsAsync(
        AvailableSlotsRequest request,
        CancellationToken cancellationToken = default);
}

