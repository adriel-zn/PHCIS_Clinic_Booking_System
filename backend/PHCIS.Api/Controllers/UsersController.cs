using System.ComponentModel.DataAnnotations;
using System.Data;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PHCIS.Api.Authentication;
using PHCIS.Api.Contracts;
using PHCIS.Infrastructure.Identity;
using PHCIS.Infrastructure.Persistence;

namespace PHCIS.Api.Controllers;

/// <summary>Account administration and clinic-scoped patient profile editing.</summary>
[ApiController, Route("api/users"), Authorize(Policy = PatientIdentity.Policy)]
public sealed class UsersController(PhcisDbContext context, UserManager<PortalUser> users, AccountService accounts,
    AccountAccess access, TimeProvider clock, ILogger<UsersController> logger) : ControllerBase
{
    /// <summary>Reports the current account's user-management capabilities.</summary>
    [HttpGet("access")]
    public async Task<IActionResult> Access() => Ok(new
    {
        CanManageUsers = await access.HasRoleAsync(User, PortalRoles.Admin),
        CanEditPatients = await access.HasRoleAsync(User, PortalRoles.Staff)
    });

    private async Task<IQueryable<PortalUser>?> VisibleAsync()
    {
        if (await access.HasRoleAsync(User, PortalRoles.Admin))
            return context.Users;
        if (!await access.HasRoleAsync(User, PortalRoles.Staff))
            return null;
        var actorId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var privilegedRoles = context.Roles.Where(r => r.Name == PortalRoles.Admin || r.Name == PortalRoles.Staff).Select(r => r.Id);
        return context.Users.Where(u => !u.IsDeleted && !context.UserRoles.Any(ur => ur.UserId == u.Id && privilegedRoles.Contains(ur.RoleId)) &&
            context.Appointments.Any(a => a.PatientId == u.PatientId && context.StaffClinics.Any(sc => sc.UserId == actorId && sc.ClinicId == a.TimeSlot.ClinicId)));
    }

    /// <summary>Lists records within the caller's current authorization scope.</summary>
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] int offset = 0, [FromQuery] int limit = 50, [FromQuery] string? search = null)
    {
        var query = await VisibleAsync();
        if (query is null)
            return Forbid();
        if (offset < 0 || limit is < 1 or > 100 || search?.Length > 100)
            return BadRequest();
        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(u => u.Email != null && u.Email.Contains(search));
        var currentId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var rows = await (from u in query
                          join p in context.Patients on u.PatientId equals p.Id
                          orderby u.Email, u.Id
                          select new { u.Id, u.Email, p.FirstName, p.LastName, p.PhoneNumber, p.DateOfBirth, u.IsActive, u.IsDeleted, IsCurrentUser = u.Id == currentId })
            .Skip(offset).Take(limit + 1).ToListAsync();
        return Ok(new { Items = rows.Take(limit), HasMore = rows.Count > limit, Offset = offset, Limit = limit });
    }

    /// <summary>Creates a registered patient account for an authorized administrator.</summary>
    [HttpPost, Authorize(Policy = ClinicAdministration.Policy)]
    public async Task<IActionResult> Create(RegisterBody body, CancellationToken ct)
    {
        await accounts.RegisterAsync(body, ct);
        logger.LogInformation("Administrator {ActorId} created an account.", User.FindFirstValue(ClaimTypes.NameIdentifier));
        return StatusCode(201);
    }

    /// <summary>Updates a visible patient profile without changing credentials or roles.</summary>
    [HttpPost("{accountId}/profile")]
    public async Task<IActionResult> Edit(string accountId, ManagedProfileBody body)
    {
        var visible = await VisibleAsync();
        if (visible is null)
            return Forbid();
        var user = await visible.SingleOrDefaultAsync(u => u.Id == accountId);
        if (user is null || user.IsDeleted)
            return NotFound();
        var patient = await context.Patients.SingleAsync(p => p.Id == user.PatientId);
        patient.UpdateDemographics(body.FirstName, body.LastName, body.DateOfBirth, clock);
        patient.UpdateContactDetails(body.PhoneNumber, patient.Email);
        await context.SaveChangesAsync();
        logger.LogInformation("Account {TargetId} profile updated by {ActorId}.", accountId, User.FindFirstValue(ClaimTypes.NameIdentifier));
        return NoContent();
    }

    /// <summary>Changes account state while protecting the last active administrator.</summary>
    [HttpPost("{accountId}/state"), Authorize(Policy = ClinicAdministration.Policy)]
    public async Task<IActionResult> SetState(string accountId, AccountStateBody body)
    {
        await AccountAdministrationGate.Changes.WaitAsync();
        try
        {
            // Lock the Admin role row across API instances while counting active administrators.
            await context.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
            {
                await using var tx = context.Database.IsRelational() ? await context.Database.BeginTransactionAsync(IsolationLevel.Serializable) : null;
                var user = await users.FindByIdAsync(accountId) ?? throw new PHCIS.Application.Exceptions.ResourceNotFoundException("Account was not found.");
                if (user.IsDeleted && !body.Delete)
                    throw new PHCIS.Domain.Exceptions.DomainRuleException("An archived account cannot be reactivated.");
                if ((!body.IsActive || body.Delete) && user.IsActive && !user.IsDeleted && await users.IsInRoleAsync(user, PortalRoles.Admin))
                {
                    var adminRole = await context.Roles.SingleAsync(r => r.NormalizedName == "ADMIN");
                    var activeAdmins = await context.Users.CountAsync(u => u.IsActive && !u.IsDeleted && context.UserRoles.Any(ur => ur.UserId == u.Id && ur.RoleId == adminRole.Id));
                    if (activeAdmins <= 1)
                        throw new PHCIS.Domain.Exceptions.DomainRuleException("The last active administrator cannot be deactivated or deleted.");
                }
                user.IsActive = body.IsActive && !body.Delete;
                user.IsDeleted |= body.Delete;
                await context.SaveChangesAsync();
                if (tx is not null)
                    await tx.CommitAsync();
            });
            logger.LogInformation("Account {TargetId} state changed by {ActorId}.", accountId, User.FindFirstValue(ClaimTypes.NameIdentifier));
            return NoContent();
        }
        finally { AccountAdministrationGate.Changes.Release(); }
    }
}
/// <summary>Editable patient demographics; credentials and role membership are excluded.</summary>
public sealed class ManagedProfileBody
{
    /// <summary>Patient first name.</summary>
    [Required, MaxLength(100)] public string FirstName { get; set; } = "";
    /// <summary>Patient last name.</summary>
    [Required, MaxLength(100)] public string LastName { get; set; } = "";
    /// <summary>Patient date of birth.</summary>
    public DateOnly DateOfBirth { get; set; }
    /// <summary>Patient contact phone number.</summary>
    [Required, MaxLength(30)] public string PhoneNumber { get; set; } = "";
}
/// <summary>Confirmed activation or archive action on a user account.</summary>
public sealed class AccountStateBody
{
    /// <summary>Whether this account should be allowed to sign in.</summary>
    public bool IsActive { get; set; }
    /// <summary>Archives the account while retaining clinical history.</summary>
    public bool Delete { get; set; }
    /// <summary>Explicit confirmation of changing account access.</summary>
    [Range(typeof(bool), "true", "true")] public bool ConfirmChange { get; set; }
}
