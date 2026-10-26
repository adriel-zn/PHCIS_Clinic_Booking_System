namespace PHCIS.Application.Models;

public sealed record PatientDto(
    Guid Id,
    string FirstName,
    string LastName,
    DateOnly DateOfBirth,
    string? PhoneNumber,
    string? Email);
