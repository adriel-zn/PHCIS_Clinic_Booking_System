using System.ComponentModel.DataAnnotations;

namespace PHCIS.Api.Contracts;

/// <summary>Validated HTTP contract for LoginBody.</summary>
public sealed record LoginBody([Required, EmailAddress] string Email, [Required] string Password, bool RememberMe = false);
/// <summary>Validated HTTP contract for RegisterBody.</summary>
public sealed record RegisterBody(
    [Required, StringLength(100)] string FirstName,
    [Required, StringLength(100)] string LastName,
    [Required, EmailAddress, StringLength(254)] string Email,
    [Required, StringLength(30)] string PhoneNumber,
    DateOnly DateOfBirth,
    [Required, StringLength(128, MinimumLength = 10)] string Password,
    [StringLength(20)] string? NationalId = null);
/// <summary>Validated HTTP contract for UpdateProfileBody.</summary>
public sealed record UpdateProfileBody(
    [Required, StringLength(100)] string FirstName,
    [Required, StringLength(100)] string LastName,
    [Required, StringLength(30)] string PhoneNumber,
    DateOnly DateOfBirth,
    [StringLength(20)] string? NationalId = null);
/// <summary>Validated HTTP contract for RescheduleBody.</summary>
public sealed record RescheduleBody(Guid TimeSlotId);

