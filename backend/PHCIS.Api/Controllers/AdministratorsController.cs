using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PHCIS.Api.Authentication;

namespace PHCIS.Api.Controllers;

/// <summary>Existing administrators can explicitly authorize other registered users.</summary>
[ApiController]
[Route("api/administrators")]
[Authorize(Policy = PatientIdentity.Policy)]
[Authorize(Policy = ClinicAdministration.Policy)]
public sealed class AdministratorsController(AdministratorService service) : ControllerBase
{
    /// <summary>Lists current Admin-role accounts and whether removal is available.</summary>
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<AdministratorDetails>>> List() =>
        Ok(await service.ListAsync(User.FindFirstValue(ClaimTypes.NameIdentifier)!));

    /// <summary>Removes Admin membership; the final administrator is protected on the server.</summary>
    [HttpPost("{accountId}/removal")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Remove(string accountId, RemoveAdministratorBody request)
    {
        await service.RemoveAsync(accountId, User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        return NoContent();
    }
    /// <summary>Grants clinic administrator permission; the recipient must sign out and back in.</summary>
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Grant(GrantAdministratorBody request)
    {
        await service.GrantAsync(request.Email, User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        return NoContent();
    }
}

/// <summary>Explicit confirmation of removing privileged access, without deleting the account.</summary>
public sealed class RemoveAdministratorBody
{
    /// <summary>Confirms removal of administrator access for the selected account.</summary>
    [Range(typeof(bool), "true", "true", ErrorMessage = "Confirm removal of administrator access.")]
    public bool ConfirmRemoval { get; set; }
}

/// <summary>Requires explicit confirmation of the new account's privileged access.</summary>
public sealed class GrantAdministratorBody
{
    /// <summary>Email of the registered account receiving access.</summary>
    [Required, EmailAddress, MaxLength(254)]
    public string Email { get; set; } = "";
    /// <summary>Explicit confirmation that the target account should receive privileged access.</summary>
    [Range(typeof(bool), "true", "true", ErrorMessage = "Confirm that this account should receive administrator access.")]
    public bool ConfirmAdministratorAccess { get; set; }
}
