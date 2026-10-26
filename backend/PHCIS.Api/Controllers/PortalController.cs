using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PHCIS.Api.Authentication;
using PHCIS.Application.Abstractions;
using PHCIS.Application.Models;

namespace PHCIS.Api.Controllers;

/// <summary>Patient-scoped dashboard read models and the clinic directory.</summary>
[ApiController]
[Route("api")]
[Authorize(Policy = PatientIdentity.Policy)]
public sealed class PortalController(IPortalReader portal) : ControllerBase
{
    /// <summary>Reads a clinic's slot for the booking review; booking still checks live availability.</summary>
    [HttpGet("clinics/{clinicId:guid}/slots/{slotId:guid}")]
    public async Task<ActionResult<TimeSlotDto>> Slot(Guid clinicId, Guid slotId, CancellationToken cancellationToken)
    {
        var slot = await portal.SlotAsync(clinicId, slotId, cancellationToken);
        return slot is null ? NotFound() : Ok(slot);
    }
    /// <summary>Lists clinics without exposing patient data.</summary>
    [HttpGet("clinics")]
    public async Task<IReadOnlyList<ClinicCard>> Clinics(CancellationToken cancellationToken) =>
        await portal.ClinicsAsync(cancellationToken);

    /// <summary>Lists only the authenticated patient's appointment history.</summary>
    [HttpGet("appointments")]
    public async Task<IReadOnlyList<AppointmentDetails>> Appointments(CancellationToken cancellationToken)
    {
        Response.Headers.CacheControl = "no-store";
        PatientIdentity.TryGetPatientId(User, out var patientId);
        return await portal.AppointmentsAsync(patientId, cancellationToken);
    }
}

