using Microsoft.AspNetCore.Identity;

namespace PHCIS.Infrastructure.Identity;

/// <summary>Identity owns credentials; the linked Domain patient owns clinical scheduling details.</summary>
public sealed class PortalUser : IdentityUser
{
    public Guid PatientId { get; set; }
    public bool IsActive { get; set; } = true;
    public bool IsDeleted { get; set; }
}

