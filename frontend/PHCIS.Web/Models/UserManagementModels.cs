using System.ComponentModel.DataAnnotations;
namespace PHCIS.Web.Models;

public sealed record UserManagementAccess(bool CanManageUsers, bool CanEditPatients);
public sealed record ManagedUser(string Id, string Email, string FirstName, string LastName, string PhoneNumber,
    DateOnly DateOfBirth, bool IsActive, bool IsDeleted, bool IsCurrentUser = false);
public sealed record ManagedUsersPage(IReadOnlyList<ManagedUser> Items, bool HasMore, int Offset, int Limit);
public sealed class ManagedProfileModel
{
    [Required, StringLength(100)] public string FirstName { get; set; } = "";
    [Required, StringLength(100)] public string LastName { get; set; } = "";
    [Required, StringLength(30)] public string PhoneNumber { get; set; } = "";
    public DateOnly DateOfBirth { get; set; }
}
