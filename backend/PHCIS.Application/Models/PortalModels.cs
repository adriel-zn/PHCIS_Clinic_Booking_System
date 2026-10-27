namespace PHCIS.Application.Models;

public sealed record ClinicCard(Guid Id, string Name, string Address, string? PhoneNumber);
public sealed record PatientProfile(Guid Id, string FirstName, string LastName, string? Email,
    string? PhoneNumber, DateOnly DateOfBirth, string? NationalId);
public sealed record AppointmentDetails(Guid Id, Guid TimeSlotId, Guid ClinicId, string ClinicName,
    string Address, DateTimeOffset StartsAt, DateTimeOffset EndsAt, string Status, string ReasonForVisit);

