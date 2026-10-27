using System.ComponentModel.DataAnnotations;

namespace PHCIS.Web.Models;

/// <summary>Editable clinic details; the API supplies initial availability.</summary>
public sealed class CreateClinicModel
{
    [Required, StringLength(200)]
    public string Name { get; set; } = "";
    [Required, StringLength(500)]
    public string Address { get; set; } = "";
    [StringLength(30)]
    public string? PhoneNumber { get; set; }
}

public sealed record ClinicManagementAccess(bool CanManageClinics);

public sealed record ManagedTimeSlot(Guid Id, DateTimeOffset StartsAt, DateTimeOffset EndsAt,
    bool IsBlocked, bool IsBooked, bool HasAppointments);

public sealed class SlotScheduleModel
{
    [Required]
    public DateTime? StartsAt { get; set; }
    [Required]
    public DateTime? EndsAt { get; set; }
}

public sealed class GrantAdministratorModel
{
    [Required, EmailAddress, StringLength(254)]
    public string Email { get; set; } = "";
    [Range(typeof(bool), "true", "true", ErrorMessage = "Confirm administrator access before continuing.")]
    public bool ConfirmAdministratorAccess { get; set; }
}
