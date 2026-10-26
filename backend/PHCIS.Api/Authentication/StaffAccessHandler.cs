using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using PHCIS.Infrastructure.Identity;

namespace PHCIS.Api.Authentication;

/// <summary>Uses current Staff membership so removing the role also revokes existing sessions' staff access.</summary>
public sealed class StaffAccessHandler(UserManager<PortalUser> users) : AuthorizationHandler<StaffAccessRequirement>
{
    /// <inheritdoc />
    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, StaffAccessRequirement requirement)
    {
        var id = context.User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (context.User.Identity?.IsAuthenticated != true || string.IsNullOrWhiteSpace(id))
            return;
        var user = await users.FindByIdAsync(id);
        if (user is not null && await users.IsInRoleAsync(user, PortalRoles.Staff))
            context.Succeed(requirement);
    }
}

/// <summary>Requires current membership in the persisted Staff role.</summary>
public sealed class StaffAccessRequirement : IAuthorizationRequirement
{
    /// <summary>Staff dashboard policy name.</summary>
    public const string Policy = "ClinicStaff";
}
