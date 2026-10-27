using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using PHCIS.Infrastructure.Identity;

namespace PHCIS.Api.Authentication;

/// <summary>Only server-managed account linkage can establish the patient identity.</summary>
public sealed class PatientClaimsFactory(UserManager<PortalUser> users, RoleManager<IdentityRole> roles, IOptions<IdentityOptions> options)
    : UserClaimsPrincipalFactory<PortalUser, IdentityRole>(users, roles, options)
{
    /// <inheritdoc />
    protected override async Task<ClaimsIdentity> GenerateClaimsAsync(PortalUser user)
    {
        var identity = await base.GenerateClaimsAsync(user);
        identity.AddClaim(new Claim(PatientIdentity.ClaimType, user.PatientId.ToString()));
        return identity;
    }
}

