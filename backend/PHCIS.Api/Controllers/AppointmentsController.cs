using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PHCIS.Api.Authentication;
using PHCIS.Api.Contracts;
using PHCIS.Application.Models;
using PHCIS.Application.Services;

namespace PHCIS.Api.Controllers;

/// <summary>HTTP adapter for patient-scoped appointment operations.</summary>
[ApiController]
[Route("api/appointments")]
[Authorize(Policy = PatientIdentity.Policy)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status500InternalServerError)]
public sealed class AppointmentsController(IAppointmentService appointments) : ControllerBase
{
    /// <summary>Books the authenticated patient into a future, unoccupied time slot.</summary>
    /// <remarks>Returns the saved appointment. This operation is not idempotent.</remarks>
    /// <response code="201">The appointment was created.</response>
    /// <response code="400">The body or identifiers are invalid.</response>
    /// <response code="404">The patient or time slot does not exist.</response>
    /// <response code="409">The slot is occupied, has started, or a concurrent write conflicted.</response>
    [HttpPost]
    [Consumes("application/json")]
    [ProducesResponseType<AppointmentDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status415UnsupportedMediaType)]
    public async Task<ActionResult<AppointmentDto>> Book(
        [FromBody] BookAppointmentBody body,
        CancellationToken cancellationToken)
    {
        PatientIdentity.TryGetPatientId(User, out var patientId);
        var result = await appointments.BookAsync(
            new BookAppointmentRequest(patientId, body.TimeSlotId, body.ReasonForVisit), cancellationToken);

        // No Location is invented: a single-appointment GET endpoint is outside this scope.
        return StatusCode(StatusCodes.Status201Created, result);
    }

    /// <summary>Cancels the authenticated patient's scheduled appointment before its start.</summary>
    /// <remarks>Requires a nonblank reason of at most 500 characters after trimming. Cancellation is terminal.</remarks>
    /// <response code="200">The appointment was cancelled and cancellation metadata is returned.</response>
    /// <response code="400">The body or cancellation reason is invalid.</response>
    /// <response code="404">The appointment does not exist for the authenticated patient.</response>
    /// <response code="409">Cancellation is too late, the state is terminal, or a concurrent write conflicted.</response>
    [HttpPost("{appointmentId:guid}/cancellation")]
    [Consumes("application/json")]
    [ProducesResponseType<AppointmentDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status415UnsupportedMediaType)]
    public async Task<ActionResult<AppointmentDto>> Cancel(
        Guid appointmentId,
        [FromBody] CancelAppointmentBody body,
        CancellationToken cancellationToken)
    {
        PatientIdentity.TryGetPatientId(User, out var patientId);
        return Ok(await appointments.CancelAsync(
            new CancelAppointmentRequest(appointmentId, patientId, body.Reason), cancellationToken));
    }

    /// <summary>Reschedules a future appointment to another available slot at the same clinic.</summary>
    [HttpPost("{appointmentId:guid}/reschedule")]
    [ProducesResponseType<AppointmentDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<AppointmentDto>> Reschedule(Guid appointmentId, RescheduleBody body, CancellationToken cancellationToken)
    {
        PatientIdentity.TryGetPatientId(User, out var patientId);
        return Ok(await appointments.RescheduleAsync(
            new RescheduleAppointmentRequest(appointmentId, patientId, body.TimeSlotId), cancellationToken));
    }
}

