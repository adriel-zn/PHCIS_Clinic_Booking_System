using System.ComponentModel.DataAnnotations;

namespace PHCIS.Web.Models;

public sealed record ClinicCard(Guid Id, string Name, string Address, string? PhoneNumber);
public sealed record PatientProfile(Guid Id, string FirstName, string LastName, string? Email,
    string? PhoneNumber, DateOnly DateOfBirth, string? NationalId)
{
    public string FullName => $"{FirstName} {LastName}";
}
public sealed record AppointmentDetails(Guid Id, Guid TimeSlotId, Guid ClinicId, string ClinicName,
    string Address, DateTimeOffset StartsAt, DateTimeOffset EndsAt, string Status, string ReasonForVisit);

public sealed class LoginModel
{
    [Required, EmailAddress] public string Email { get; set; } = "";
    [Required] public string Password { get; set; } = "";
    public bool RememberMe { get; set; }
}
public sealed class RegisterModel
{
    [Required, StringLength(100)] public string FirstName { get; set; } = "";
    [Required, StringLength(100)] public string LastName { get; set; } = "";
    [Required, EmailAddress, StringLength(254)] public string Email { get; set; } = "";
    [Required, StringLength(30)] public string PhoneNumber { get; set; } = "";
    [Required] public DateOnly? DateOfBirth { get; set; }
    [StringLength(20)] public string? NationalId { get; set; }
    [Required, StringLength(128, MinimumLength = 10)] public string Password { get; set; } = "";
    [Required, Compare(nameof(Password), ErrorMessage = "Passwords must match.")] public string ConfirmPassword { get; set; } = "";
}
public sealed class ProfileModel
{
    [Required, StringLength(100)] public string FirstName { get; set; } = "";
    [Required, StringLength(100)] public string LastName { get; set; } = "";
    [Required, StringLength(30)] public string PhoneNumber { get; set; } = "";
    [Required] public DateOnly? DateOfBirth { get; set; }
    [StringLength(20)] public string? NationalId { get; set; }
}

