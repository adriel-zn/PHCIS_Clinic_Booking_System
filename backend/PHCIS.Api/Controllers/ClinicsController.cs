using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PHCIS.Api.Authentication;
using PHCIS.Api.Contracts;
using PHCIS.Application.Models;
using PHCIS.Application.Services;

namespace PHCIS.Api.Controllers;

/// <summary>HTTP adapter for clinic slot availability.</summary>
[ApiController]
[Route("api/clinics")]
[Authorize(Policy = PatientIdentity.Policy)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status500InternalServerError)]
public sealed class ClinicsController(IAvailabilityService availability) : ControllerBase
{
    /// <summary>Lists future, unoccupied clinic slots ordered by start time and ID.</summary>
    /// <remarks>
    /// Slot starts are filtered using From inclusive and To exclusive. Limit defaults to 50
    /// and must be 1–100; Offset must be nonnegative. Results are snapshots, not reservations.
    /// Supply timestamps with Z or an explicit UTC offset.
    /// </remarks>
    /// <response code="200">A page of available slots; Items may be empty.</response>
    /// <response code="400">The clinic ID, interval, or pagination is invalid.</response>
    /// <response code="404">The clinic does not exist.</response>
    [HttpGet("{clinicId:guid}/available-slots")]
    [ProducesResponseType<AvailableSlotsDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AvailableSlotsDto>> GetAvailableSlots(
        Guid clinicId,
        [FromQuery] AvailableSlotsQuery query,
        CancellationToken cancellationToken)
    {
        return Ok(await availability.GetAvailableSlotsAsync(
            new AvailableSlotsRequest(clinicId, query.From!.Value, query.To!.Value, query.Offset, query.Limit),
            cancellationToken));
    }
}

