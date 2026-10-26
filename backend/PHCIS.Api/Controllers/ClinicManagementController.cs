using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PHCIS.Api.Authentication;
using PHCIS.Application.Services;

namespace PHCIS.Api.Controllers;

/// <summary>Clinic administration protected independently of patient booking access.</summary>
[ApiController]
[Route("api/clinic-management")]
[Authorize(Policy = PatientIdentity.Policy)]
public sealed class ClinicManagementController(IClinicManagementService service, IConfiguration configuration) : ControllerBase
{
    /// <summary>Reports whether the current account may manage clinics.</summary>
    [HttpGet("access")]
    public IActionResult Access() => Ok(new { CanManageClinics = ClinicAdministration.IsAllowed(User, configuration) });

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
