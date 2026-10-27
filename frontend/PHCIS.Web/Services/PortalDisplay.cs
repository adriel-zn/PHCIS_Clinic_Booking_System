using PHCIS.Web.Models;
namespace PHCIS.Web.Services;

public static class PortalDisplay
{
    public static string Date(DateTimeOffset time) => time.ToOffset(TimeSpan.FromHours(2)).ToString("dd MMM yyyy");
    public static string Time(DateTimeOffset time) => time.ToOffset(TimeSpan.FromHours(2)).ToString("HH:mm");
    public static string Reference(Guid id) => $"PHC-{id.ToString("N")[..12].ToUpperInvariant()}";
    public static string Initials(PatientProfile? profile) => profile is null ? "P" : $"{profile.FirstName[0]}{profile.LastName[0]}".ToUpperInvariant();
}

