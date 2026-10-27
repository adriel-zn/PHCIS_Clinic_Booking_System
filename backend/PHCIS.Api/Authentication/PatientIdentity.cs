using System.Security.Claims;

namespace PHCIS.Api.Authentication;

/// <summary>Resolves exactly one nonempty patient ID from an authenticated identity.</summary>
public static class PatientIdentity
{
    /// <summary>Authorization policy required by patient API endpoints.</summary>
    public const string Policy = "Patient";
    /// <summary>Issuer-managed claim identifying a persisted patient.</summary>
    public const string ClaimType = "patient_id";

    /// <summary>Rejects missing, malformed, empty, or ambiguous patient claims.</summary>
    public static bool TryGetPatientId(ClaimsPrincipal principal, out Guid patientId)
    {
        patientId = Guid.Empty;
        var claims = principal.FindAll(ClaimType).Take(2).ToArray();
        return principal.Identity?.IsAuthenticated == true &&
            claims.Length == 1 &&
            Guid.TryParse(claims[0].Value, out patientId) &&
            patientId != Guid.Empty;
    }
}

