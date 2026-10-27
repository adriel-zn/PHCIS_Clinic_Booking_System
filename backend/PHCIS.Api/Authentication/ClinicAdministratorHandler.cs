using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using PHCIS.Infrastructure.Identity;

namespace PHCIS.Api.Authentication;

/// <summary>Checks live membership so stale session cookies cannot retain revoked access.</summary>
public sealed class ClinicAdministratorHandler(UserManager<PortalUser> users)
    : AuthorizationHandler<ClinicAdministratorRequirement>
{
    /// <inheritdoc />
    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, ClinicAdministratorRequirement requirement)
    {
        var id = context.User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (context.User.Identity?.IsAuthenticated != true || string.IsNullOrWhiteSpace(id))
            return;
        var user = await users.FindByIdAsync(id);
        if (user is { IsActive: true, IsDeleted: false } && await users.IsInRoleAsync(user, PortalRoles.Admin))
            context.Succeed(requirement);
    }
}

/// <summary>Requires current membership in the persisted Admin role.</summary>
public sealed class ClinicAdministratorRequirement : IAuthorizationRequirement { }
