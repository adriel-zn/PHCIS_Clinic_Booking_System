using PHCIS.Domain.Common;
using PHCIS.Domain.Exceptions;

namespace PHCIS.Domain.Entities;

public sealed class Patient
{
    private readonly List<Appointment> _appointments = [];
    private byte[] _rowVersion = [];

    private Patient() { }

    public Patient(string firstName, string lastName, DateOnly dateOfBirth, TimeProvider timeProvider)
    {
        Id = Guid.NewGuid();
        UpdateDemographics(firstName, lastName, dateOfBirth, timeProvider);
    }

    public Guid Id { get; private set; }
    public string FirstName { get; private set; } = string.Empty;
    public string LastName { get; private set; } = string.Empty;
    public DateOnly DateOfBirth { get; private set; }
    public string? PhoneNumber { get; private set; }
    public string? Email { get; private set; }
    public string? NationalId { get; private set; }

    public void UpdateNationalId(string? nationalId)
    {
        NationalId = DomainGuard.OptionalText(nationalId, 20, "ID number");
    }

    public byte[] RowVersion => (byte[])_rowVersion.Clone();

    public IReadOnlyCollection<Appointment> Appointments => _appointments.AsReadOnly();

    public void UpdateDemographics(
        string firstName,
        string lastName,
        DateOnly dateOfBirth,
        TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);
        var normalizedFirstName = DomainGuard.RequiredText(firstName, 100, "First name");
        var normalizedLastName = DomainGuard.RequiredText(lastName, 100, "Last name");
        var today = DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime);

        if (dateOfBirth == default || dateOfBirth > today)
        {
            throw new DomainRuleException("Date of birth must be specified and must not be in the future.");
        }

        FirstName = normalizedFirstName;
        LastName = normalizedLastName;
        DateOfBirth = dateOfBirth;
    }

    public void UpdateContactDetails(string? phoneNumber, string? email)
    {
        var normalizedPhone = DomainGuard.OptionalText(phoneNumber, 30, "Phone number");
        var normalizedEmail = DomainGuard.Email(email);

        PhoneNumber = normalizedPhone;
        Email = normalizedEmail;
    }
}

