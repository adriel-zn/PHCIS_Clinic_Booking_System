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
