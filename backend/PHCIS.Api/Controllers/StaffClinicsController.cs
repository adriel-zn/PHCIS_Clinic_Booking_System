using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PHCIS.Api.Authentication;
using PHCIS.Infrastructure.Identity;
using PHCIS.Infrastructure.Persistence;

namespace PHCIS.Api.Controllers;

/// <summary>Administrators explicitly assign and withdraw Staff clinic scopes.</summary>
[ApiController, Route("api/staff/{accountId}/clinics")]
[Authorize(Policy = PatientIdentity.Policy), Authorize(Policy = ClinicAdministration.Policy)]
public sealed class StaffClinicsController(PhcisDbContext context, UserManager<PortalUser> users,
    ILogger<StaffClinicsController> logger) : ControllerBase
{
    /// <summary>Lists records within the caller's current authorization scope.</summary>
    [HttpGet]
    public async Task<IActionResult> List(string accountId) => Ok(await context.Clinics
        .Where(c => context.StaffClinics.Any(x => x.UserId == accountId && x.ClinicId == c.Id))
        .Select(c => new PHCIS.Application.Models.ClinicCard(c.Id, c.Name, c.Address, c.PhoneNumber)).ToListAsync());

    /// <summary>Saves an explicitly confirmed clinic assignment for a Staff account.</summary>
    [HttpPost]
    public async Task<IActionResult> Assign(string accountId, ClinicAssignmentBody body)
    {
        var user = await users.FindByIdAsync(accountId);
        if (user is not { IsActive: true, IsDeleted: false } || !await users.IsInRoleAsync(user, PortalRoles.Staff))
            return NotFound();
        if (!await context.Clinics.AnyAsync(x => x.Id == body.ClinicId))
            return NotFound();
        if (!await context.StaffClinics.AnyAsync(x => x.UserId == accountId && x.ClinicId == body.ClinicId))
        {
            context.StaffClinics.Add(new StaffClinic { UserId = accountId, ClinicId = body.ClinicId });
            try
            { await context.SaveChangesAsync(); }
            catch (DbUpdateException e) when (e.InnerException is Microsoft.Data.SqlClient.SqlException { Number: 2601 or 2627 }) { }
        }
        logger.LogInformation("Staff clinic assignment saved for {TargetId} at {ClinicId}.", accountId, body.ClinicId);
        return NoContent();
    }

    /// <summary>Withdraws a clinic assignment without deleting users or appointments.</summary>
    [HttpPost("{clinicId:guid}/removal")]
    public async Task<IActionResult> Remove(string accountId, Guid clinicId, ClinicScopeRemoval body)
    {
        var assignment = await context.StaffClinics.FindAsync(accountId, clinicId);
        if (assignment is not null)
        { context.StaffClinics.Remove(assignment); await context.SaveChangesAsync(); }
        logger.LogInformation("Staff clinic assignment removed for {TargetId} at {ClinicId}.", accountId, clinicId);
        return NoContent();
    }
}
/// <summary>Confirmed addition of a clinic to a staff account's scope.</summary>
public sealed class ClinicAssignmentBody
{
    /// <summary>Clinic identifier for the assignment.</summary>
    public Guid ClinicId { get; set; }
    /// <summary>Explicit confirmation of the clinic assignment.</summary>
    [Range(typeof(bool), "true", "true")] public bool ConfirmAssignment { get; set; }
}
/// <summary>Confirmation of withdrawing a staff clinic assignment.</summary>
public sealed class ClinicScopeRemoval
{
    /// <summary>Explicit confirmation of removing the clinic assignment.</summary>
    [Range(typeof(bool), "true", "true")] public bool ConfirmRemoval { get; set; }
}
