using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PHCIS.Api.Authentication;
using PHCIS.Api.Contracts;
using PHCIS.Application.Abstractions;
using PHCIS.Application.Models;
using PHCIS.Infrastructure.Identity;
using PHCIS.Infrastructure.Persistence;

namespace PHCIS.Api.Controllers;

/// <summary>Local EF Identity registration and HttpOnly-cookie sessions.</summary>
[ApiController]
[Route("api/account")]
public sealed class AccountController(
    AccountService accounts,
    SignInManager<PortalUser> signIn,
    IPortalReader portal,
    PhcisDbContext context,
    TimeProvider clock) : ControllerBase
{
    /// <summary>Obtains a request token; send it as X-CSRF-TOKEN on every unsafe request.</summary>
    [HttpGet("csrf")]
    [AllowAnonymous]
    public IActionResult Csrf([FromServices] IAntiforgery antiforgery)
    {
        Response.Headers.CacheControl = "no-cache, no-store";
        Response.Headers.Pragma = "no-cache";
        return Ok(new { requestToken = antiforgery.GetAndStoreTokens(HttpContext).RequestToken });
    }

    /// <summary>Creates an Identity account and its linked patient atomically.</summary>
    [HttpPost("register")]
    [AllowAnonymous]
    public async Task<IActionResult> Register(RegisterBody body, CancellationToken cancellationToken)
    {
        await accounts.RegisterAsync(body, cancellationToken);
        return StatusCode(StatusCodes.Status201Created);
    }

    /// <summary>Checks the Identity password and creates a secure HttpOnly session cookie.</summary>
    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<IActionResult> Login(LoginBody body)
    {
        if (!await accounts.LoginAsync(body))
        {
            return Problem(statusCode: 401, title: "Sign-in failed.", detail: "Email or password is incorrect.");
        }
        return Ok();
    }

    /// <summary>Ends the current local session.</summary>
    [HttpPost("logout")]
    [Authorize(Policy = PatientIdentity.Policy)]
    public async Task<IActionResult> Logout()
    {
        await signIn.SignOutAsync();
        return NoContent();
    }

    /// <summary>Returns the signed-in patient's own profile.</summary>
    [HttpGet("me")]
    [Authorize(Policy = PatientIdentity.Policy)]
    public async Task<ActionResult<PatientProfile>> Me(CancellationToken cancellationToken)
    {
        Response.Headers.CacheControl = "no-store";
        PatientIdentity.TryGetPatientId(User, out var patientId);
        var profile = await portal.ProfileAsync(patientId, cancellationToken);
        return profile is null ? NotFound() : Ok(profile);
    }

    /// <summary>Updates demographics; account email is read-only in this local development flow.</summary>
    [HttpPost("profile")]
    [Authorize(Policy = PatientIdentity.Policy)]
    public async Task<ActionResult<PatientProfile>> UpdateProfile(UpdateProfileBody body, CancellationToken cancellationToken)
    {
        PatientIdentity.TryGetPatientId(User, out var patientId);
        var patient = await context.Patients.SingleOrDefaultAsync(x => x.Id == patientId, cancellationToken);
        if (patient is null)
        {
            return NotFound();
        }
        patient.UpdateDemographics(body.FirstName, body.LastName, body.DateOfBirth, clock);
        patient.UpdateContactDetails(body.PhoneNumber, patient.Email);
        patient.UpdateNationalId(body.NationalId);
        await context.SaveChangesAsync(cancellationToken);
        return Ok(await portal.ProfileAsync(patientId, cancellationToken));
    }
}

