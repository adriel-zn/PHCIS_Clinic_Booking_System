namespace PHCIS.Infrastructure.Identity;

/// <summary>Explicit clinic scope for a Staff account; membership alone grants no clinic access.</summary>
public sealed class StaffClinic
{
    public string UserId { get; set; } = "";
    public Guid ClinicId { get; set; }
}
