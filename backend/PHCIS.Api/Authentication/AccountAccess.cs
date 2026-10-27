using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using PHCIS.Application.Models;
using PHCIS.Infrastructure.Identity;
using PHCIS.Infrastructure.Persistence;

namespace PHCIS.Api.Authentication;

/// <summary>Live account and clinic scope checks shared by REST, GraphQL and real-time delivery.</summary>
public sealed class AccountAccess(UserManager<PortalUser> users, PhcisDbContext context)
{
    /// <summary>Returns the current active, non-archived account.</summary>
    public async Task<PortalUser?> UserAsync(ClaimsPrincipal principal)
    {
        var id = principal.FindFirstValue(ClaimTypes.NameIdentifier);
        var user = id is null ? null : await users.FindByIdAsync(id);
        return user is { IsActive: true, IsDeleted: false } ? user : null;
    }
    /// <summary>Checks live role membership for an active account.</summary>
    public async Task<bool> HasRoleAsync(ClaimsPrincipal principal, string role)
    {
        var user = await UserAsync(principal);
        return user is not null && await users.IsInRoleAsync(user, role);
    }
    /// <summary>Checks current Staff membership and explicit clinic assignment.</summary>
    public async Task<bool> ClinicAsync(ClaimsPrincipal principal, Guid clinicId) =>
        await HasRoleAsync(principal, PortalRoles.Staff) && await context.StaffClinics.AnyAsync(x =>
            x.UserId == principal.FindFirstValue(ClaimTypes.NameIdentifier) && x.ClinicId == clinicId);
    /// <summary>Returns only the current staff account's assigned clinics.</summary>
    public async Task<IReadOnlyList<ClinicCard>> ClinicsAsync(ClaimsPrincipal principal)
    {
        if (!await HasRoleAsync(principal, PortalRoles.Staff))
            return [];
        var id = principal.FindFirstValue(ClaimTypes.NameIdentifier);
        return await context.Clinics.Where(c => context.StaffClinics.Any(x => x.UserId == id && x.ClinicId == c.Id))
            .OrderBy(x => x.Name).Select(x => new ClinicCard(x.Id, x.Name, x.Address, x.PhoneNumber)).ToListAsync();
    }
}

/// <summary>Requires a currently active, non-archived Identity account.</summary>
public sealed class ActiveAccountRequirement : IAuthorizationRequirement { }
/// <summary>Rejects inactive accounts even when their authentication cookie is still valid.</summary>
public sealed class ActiveAccountHandler(AccountAccess access) : AuthorizationHandler<ActiveAccountRequirement>
{
    /// <inheritdoc />
    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, ActiveAccountRequirement requirement)
    {
        if (await access.UserAsync(context.User) is not null)
            context.Succeed(requirement);
    }
}
