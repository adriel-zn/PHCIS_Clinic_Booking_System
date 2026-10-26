using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PHCIS.Api.Authentication;
using PHCIS.Application.Models;
using PHCIS.Application.Services;

namespace PHCIS.Api.Controllers;

/// <summary>Staff-only clinic appointment viewing and terminal status transitions.</summary>
[ApiController]
[Route("api/staff-appointments")]
[Authorize(Policy = PatientIdentity.Policy)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
public sealed class StaffAppointmentsController(IStaffAppointmentService service, IAuthorizationService authorization,
    ILogger<StaffAppointmentsController> logger) : ControllerBase
{
    /// <summary>Reports current Staff dashboard access.</summary>
    [HttpGet("access")]
    public async Task<IActionResult> Access() => Ok(new
    {
        CanViewAppointments = (await authorization.AuthorizeAsync(User, StaffAccessRequirement.Policy)).Succeeded
    });

    /// <summary>Lists a clinic's appointments on a South African calendar day with optional status and bounded paging.</summary>
    [HttpGet]
    [Authorize(Policy = StaffAccessRequirement.Policy)]
    [ProducesResponseType<StaffAppointmentsPage>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<StaffAppointmentsPage>> List([FromQuery] Guid clinicId, [FromQuery] DateOnly date,
        [FromQuery] string? status, CancellationToken cancellationToken, [FromQuery] int offset = 0, [FromQuery] int limit = 50) =>
        Ok(await service.ListAsync(new StaffAppointmentsRequest(clinicId, date, status, offset, limit), cancellationToken));

    /// <summary>Marks a Scheduled appointment Completed after its slot ends.</summary>
    [HttpPost("{clinicId:guid}/{appointmentId:guid}/completion")]
    [Authorize(Policy = StaffAccessRequirement.Policy)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Complete(Guid clinicId, Guid appointmentId, FinalizeAppointmentBody request, CancellationToken cancellationToken)
    {
        await service.CompleteAsync(clinicId, appointmentId, cancellationToken);
        LogTransition(clinicId, appointmentId, "Completed");
        return NoContent();
    }

    /// <summary>Marks a Scheduled appointment NoShow after its slot ends.</summary>
    [HttpPost("{clinicId:guid}/{appointmentId:guid}/no-show")]
    [Authorize(Policy = StaffAccessRequirement.Policy)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> NoShow(Guid clinicId, Guid appointmentId, FinalizeAppointmentBody request, CancellationToken cancellationToken)
    {
        await service.MarkNoShowAsync(clinicId, appointmentId, cancellationToken);
        LogTransition(clinicId, appointmentId, "NoShow");
        return NoContent();
    }

    private void LogTransition(Guid clinicId, Guid appointmentId, string outcome) =>
        logger.LogInformation("Staff account {ActorId} marked appointment {AppointmentId} at clinic {ClinicId} as {Outcome}.",
            User.FindFirstValue(ClaimTypes.NameIdentifier), appointmentId, clinicId, outcome);
}

/// <summary>Confirmation of a terminal appointment status change.</summary>
public sealed class FinalizeAppointmentBody
{
    /// <summary>Explicit confirmation that the selected appointment should be finalized.</summary>
    [Range(typeof(bool), "true", "true", ErrorMessage = "Confirm the appointment status change.")]
    public bool ConfirmStatusChange { get; set; }
}
