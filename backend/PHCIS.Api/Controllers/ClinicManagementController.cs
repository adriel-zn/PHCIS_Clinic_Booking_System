using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PHCIS.Api.Authentication;
using PHCIS.Application.Services;

namespace PHCIS.Api.Controllers;

/// <summary>Clinic administration protected independently of patient booking access.</summary>
[ApiController]
[Route("api/clinic-management")]
[Authorize(Policy = PatientIdentity.Policy)]
public sealed class ClinicManagementController(IClinicManagementService service, IAuthorizationService authorization) : ControllerBase
{
    /// <summary>Updates clinic contact details without changing appointments.</summary>
    [HttpPost("{clinicId:guid}/details")]
    [Authorize(Policy = ClinicAdministration.Policy)]
    public async Task<IActionResult> Update(Guid clinicId, CreateClinicRequest request, CancellationToken cancellationToken)
    {
        await service.UpdateAsync(clinicId, request, cancellationToken);
        return NoContent();
    }

    /// <summary>Lists clinic slots, including closed slots and booking state, for up to 31 days.</summary>
    [HttpGet("{clinicId:guid}/slots")]
    [Authorize(Policy = ClinicAdministration.Policy)]
    public async Task<ActionResult<IReadOnlyList<ManagedTimeSlot>>> Slots(Guid clinicId,
        [FromQuery] DateTimeOffset from, [FromQuery] DateTimeOffset to, CancellationToken cancellationToken) =>
        Ok(await service.SlotsAsync(clinicId, from, to, cancellationToken));

    /// <summary>Adds a future non-overlapping clinic slot.</summary>
    [HttpPost("{clinicId:guid}/slots")]
    [Authorize(Policy = ClinicAdministration.Policy)]
    public async Task<IActionResult> AddSlot(Guid clinicId, SlotScheduleRequest request, CancellationToken cancellationToken)
    {
        var id = await service.AddSlotAsync(clinicId, request, cancellationToken);
        return Created($"/api/clinics/{clinicId}/slots/{id}", new { Id = id });
    }

    /// <summary>Changes the times of a future slot with no appointment history.</summary>
    [HttpPost("{clinicId:guid}/slots/{slotId:guid}/schedule")]
    [Authorize(Policy = ClinicAdministration.Policy)]
    public async Task<IActionResult> UpdateSlot(Guid clinicId, Guid slotId, SlotScheduleRequest request, CancellationToken cancellationToken)
    {
        await service.UpdateSlotAsync(clinicId, slotId, request, cancellationToken);
        return NoContent();
    }

    /// <summary>Closes or reopens a future unbooked slot without deleting its history.</summary>
    [HttpPost("{clinicId:guid}/slots/{slotId:guid}/availability")]
    [Authorize(Policy = ClinicAdministration.Policy)]
    public async Task<IActionResult> Availability(Guid clinicId, Guid slotId, SlotAvailabilityBody request, CancellationToken cancellationToken)
    {
        await service.SetBlockedAsync(clinicId, slotId, request.IsBlocked, cancellationToken);
        return NoContent();
    }
    /// <summary>Reports whether the current account may manage clinics.</summary>
    [HttpGet("access")]
    public async Task<IActionResult> Access() => Ok(new
    {
        CanManageClinics = (await authorization.AuthorizeAsync(User, ClinicAdministration.Policy)).Succeeded
    });

    /// <summary>Creates a clinic and future weekday slots for the next 15 calendar days.</summary>
    /// <response code="201">Clinic and slots created.</response>
    /// <response code="400">Invalid clinic details.</response>
    /// <response code="403">The current account is not a clinic administrator.</response>
    /// <response code="409">The clinic already exists.</response>
    [HttpPost]
    [Authorize(Policy = ClinicAdministration.Policy)]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create(CreateClinicRequest request, CancellationToken cancellationToken)
    {
        var id = await service.CreateAsync(request, cancellationToken);
        return Created($"/api/clinics/{id}/available-slots", new { Id = id });
    }
}

/// <summary>Whether a slot should be withdrawn from patient availability.</summary>
public sealed record SlotAvailabilityBody(bool IsBlocked);
