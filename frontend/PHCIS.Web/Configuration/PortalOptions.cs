namespace PHCIS.Web.Configuration;

/// <summary>Public, non-secret browser configuration. South African clinics use UTC+02:00 year round.</summary>
public sealed class PortalOptions
{
    public string BaseUrl { get; set; } = "https://localhost:7014/";
    public string? DefaultClinicId { get; set; }
    public int ClinicUtcOffsetMinutes { get; set; } = 120;
    public bool UseGraphQL { get; set; }
    /// <summary>Shows the public demo account hint only for an explicitly enabled local environment.</summary>
    public bool ShowDevelopmentLogin { get; set; }

    public TimeSpan ClinicOffset => TimeSpan.FromMinutes(ClinicUtcOffsetMinutes);
    public string TimeLabel => $"UTC{(ClinicOffset < TimeSpan.Zero ? "-" : "+")}{ClinicOffset.Duration():hh\\:mm}";
}

