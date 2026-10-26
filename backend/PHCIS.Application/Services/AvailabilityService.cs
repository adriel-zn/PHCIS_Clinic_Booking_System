using AutoMapper;
using FluentValidation;
using PHCIS.Application.Abstractions;
using PHCIS.Application.Exceptions;
using PHCIS.Application.Models;

namespace PHCIS.Application.Services;

/// <summary>Returns bounded availability snapshots; results are not slot reservations.</summary>
public sealed class AvailabilityService(
    ITimeSlotRepository slots,
    IValidator<AvailableSlotsRequest> validator,
    IMapper mapper,
    TimeProvider timeProvider) : IAvailabilityService
{
    public async Task<AvailableSlotsDto> GetAvailableSlotsAsync(
        AvailableSlotsRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        await validator.ValidateAndThrowAsync(request, cancellationToken);

        if (!await slots.ClinicExistsAsync(request.ClinicId, cancellationToken))
        {
            throw new ResourceNotFoundException("Clinic was not found.");
        }

        // Read one extra row to signal another page without an unbounded count query.
        var matches = await slots.FindAvailableAsync(
            request.ClinicId,
            request.From.ToUniversalTime(),
            request.To.ToUniversalTime(),
            timeProvider.GetUtcNow(),
            request.Offset,
            request.Limit + 1,
            cancellationToken);

        var items = matches.Take(request.Limit)
            .Select(mapper.Map<TimeSlotDto>)
            .ToArray();

        return new AvailableSlotsDto(Array.AsReadOnly(items), request.Offset, request.Limit,
            matches.Count > request.Limit);
    }
}

