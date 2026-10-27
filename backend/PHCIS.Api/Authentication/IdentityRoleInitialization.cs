using Microsoft.AspNetCore.Identity;
using PHCIS.Infrastructure.Identity;

namespace PHCIS.Api.Authentication;

/// <summary>Ensures the application roles exist without restoring revoked user memberships.</summary>
public static class IdentityRoleInitialization
{
    /// <summary>Seeds role definitions in every environment; membership is managed separately.</summary>
    public static async Task InitializeAsync(WebApplication app)
    {
        await using var scope = app.Services.CreateAsyncScope();
        var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        foreach (var name in new[] { PortalRoles.Patient, PortalRoles.Admin, PortalRoles.Staff })
        {
            if (await roles.RoleExistsAsync(name))
                continue;
            var result = await roles.CreateAsync(new IdentityRole(name));
            if (!result.Succeeded && !await roles.RoleExistsAsync(name))
                throw new InvalidOperationException("Could not initialize Identity roles.");
        }
    }
}
