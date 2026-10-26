using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using PHCIS.Application.Exceptions;
using PHCIS.Domain.Exceptions;
using PHCIS.Infrastructure.Identity;

namespace PHCIS.Api.Authentication;

/// <summary>Grants server-managed permissions to existing registered accounts.</summary>
public sealed class AdministratorService(UserManager<PortalUser> users, ILogger<AdministratorService> logger)
{
    /// <summary>Grants clinic administration idempotently without changing credentials.</summary>
    public async Task GrantAsync(string email, string actorId)
    {
        var user = await users.FindByEmailAsync(email.Trim())
            ?? throw new ResourceNotFoundException("No registered account has this email. Ask the person to register first.");
        var claims = await users.GetClaimsAsync(user);
        if (claims.Any(x => x.Type == ClinicAdministration.PermissionClaim && x.Value == ClinicAdministration.PermissionValue))
            return;
        var result = await users.AddClaimAsync(user, new Claim(ClinicAdministration.PermissionClaim, ClinicAdministration.PermissionValue));
        if (!result.Succeeded)
            throw new DomainRuleException("Administrator access could not be saved. Refresh and try again.");
        logger.LogInformation("Administrator {ActorId} granted clinic administration to account {TargetId}.", actorId, user.Id);
    }
}
