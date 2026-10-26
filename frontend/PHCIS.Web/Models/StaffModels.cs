using System.ComponentModel.DataAnnotations;

namespace PHCIS.Web.Models;

public sealed record StaffDetails(string Id, string Email, bool IsCurrentUser);

/// <summary>Staff assignment requires a registered email and explicit confirmation.</summary>
public sealed class AssignStaffModel
{
    [Required, EmailAddress, StringLength(254)]
    public string Email { get; set; } = "";
    [Range(typeof(bool), "true", "true", ErrorMessage = "Confirm Staff role assignment.")]
    public bool ConfirmStaffAssignment { get; set; }
}
