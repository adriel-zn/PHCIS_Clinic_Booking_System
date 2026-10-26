using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PHCIS.Api.Authentication;

namespace PHCIS.Api.Controllers;

/// <summary>Administrator-only operations on Staff role membership.</summary>
[ApiController]
[Route("api/staff")]
[Authorize(Policy = PatientIdentity.Policy)]
[Authorize(Policy = ClinicAdministration.Policy)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
public sealed class StaffController(StaffService service) : ControllerBase
{
    /// <summary>Lists all current Staff-role members.</summary>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<StaffDetails>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<StaffDetails>>> List() =>
        Ok(await service.ListAsync(User.FindFirstValue(ClaimTypes.NameIdentifier)!));

    /// <summary>Assigns Staff to a registered account after explicit confirmation.</summary>
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Assign(AssignStaffBody request)
    {
        await service.AssignAsync(request.Email, User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        return NoContent();
    }

    /// <summary>Removes Staff membership without deleting the user or other roles.</summary>
    [HttpPost("{accountId}/removal")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Remove(string accountId, RemoveStaffBody request)
    {
        await service.RemoveAsync(accountId, User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        return NoContent();
    }
}

/// <summary>Validated, confirmed Staff assignment to a registered email address.</summary>
public sealed class AssignStaffBody
{
    /// <summary>Email of the registered user receiving Staff membership.</summary>
    [Required, EmailAddress, MaxLength(254)]
    public string Email { get; set; } = "";
    /// <summary>Explicit confirmation of the role assignment.</summary>
    [Range(typeof(bool), "true", "true", ErrorMessage = "Confirm Staff role assignment.")]
    public bool ConfirmStaffAssignment { get; set; }
}

/// <summary>Confirmation that only Staff membership should be removed.</summary>
public sealed class RemoveStaffBody
{
    /// <summary>Confirms removal of the selected account's Staff role.</summary>
    [Range(typeof(bool), "true", "true", ErrorMessage = "Confirm removal of the Staff role.")]
    public bool ConfirmRemoval { get; set; }
}
