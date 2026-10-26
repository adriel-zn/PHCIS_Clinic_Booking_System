using System.Security.Claims;

namespace PHCIS.Api.Authentication;

/// <summary>Grants clinic administration only to explicitly configured Identity accounts.</summary>
public static class ClinicAdministration
{
    /// <summary>API policy name for clinic administration.</summary>
    public const string Policy = "ClinicAdministrator";

    /// <summary>Checks the server-assigned account ID; registration cannot choose or grant this access.</summary>
    public static bool IsAllowed(ClaimsPrincipal user, IConfiguration configuration)
    {
        var accountId = user.FindFirstValue(ClaimTypes.NameIdentifier);
        return user.Identity?.IsAuthenticated == true && !string.IsNullOrWhiteSpace(accountId) &&
            (configuration.GetSection("ClinicManagement:AdministratorAccountIds").Get<string[]>() ?? [])
            .Contains(accountId, StringComparer.Ordinal);
    }
}
