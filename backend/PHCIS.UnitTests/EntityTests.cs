using PHCIS.Domain.Entities;
using PHCIS.Domain.Exceptions;

namespace PHCIS.UnitTests;

public sealed class EntityTests
{
    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(null)]
    public void PatientRequiresNames(string? name)
    {
        Assert.Throws<DomainRuleException>(() => new Patient(
            name!, "Patient", new DateOnly(2000, 1, 1), new TestClock(DomainTestData.Now)));
    }

    [Fact]
    public void InvalidDemographicUpdateLeavesOriginalState()
    {
        var patient = DomainTestData.Patient();

        Assert.Throws<DomainRuleException>(() => patient.UpdateDemographics(
            "Changed", "Name", new DateOnly(2026, 10, 27), new TestClock(DomainTestData.Now)));
        Assert.Equal("Test", patient.FirstName);
        Assert.Equal("Patient", patient.LastName);
        Assert.Equal(new DateOnly(2000, 1, 1), patient.DateOfBirth);
    }

    [Fact]
    public void PatientRejectsUnspecifiedBirthDateAndAcceptsToday()
    {
        var clock = new TestClock(DomainTestData.Now);
        Assert.Throws<DomainRuleException>(() => new Patient("A", "B", default, clock));
        var patient = new Patient(" A ", " B ", new DateOnly(2026, 10, 26), clock);
        Assert.Equal("A", patient.FirstName);
        Assert.Equal("B", patient.LastName);
    }

    [Fact]
    public void ContactUpdatesAreValidatedAndAtomic()
    {
        var patient = DomainTestData.Patient();
        patient.UpdateContactDetails(" 0123456789 ", " patient@example.com ");

        Assert.Throws<DomainRuleException>(() => patient.UpdateContactDetails("Changed", "invalid"));
        Assert.Equal("0123456789", patient.PhoneNumber);
        Assert.Equal("patient@example.com", patient.Email);

        patient.UpdateContactDetails(" ", null);
        Assert.Null(patient.PhoneNumber);
        Assert.Null(patient.Email);
    }

    [Fact]
    public void ClinicUpdateIsAtomicAndTrimsInput()
    {
        var clinic = new Clinic(" Test ", " Address ");

        Assert.Throws<DomainRuleException>(() => clinic.UpdateDetails("Changed", " "));
        Assert.Equal("Test", clinic.Name);
        Assert.Equal("Address", clinic.Address);
    }

    [Fact]
    public void PersistenceLengthLimitsAreEnforced()
    {
        var clock = new TestClock(DomainTestData.Now);
        Assert.Throws<DomainRuleException>(() =>
            new Patient(new string('x', 101), "Patient", new DateOnly(2000, 1, 1), clock));
        Assert.Throws<DomainRuleException>(() => new Clinic(new string('x', 201), "Address"));
        Assert.Throws<DomainRuleException>(() => new Clinic("Clinic", new string('x', 501)));
        Assert.Throws<DomainRuleException>(() =>
            DomainTestData.Patient().UpdateContactDetails(new string('x', 31), null));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void SlotRequiresPositiveDuration(int minutes)
    {
        Assert.Throws<DomainRuleException>(() => new TimeSlot(
            DomainTestData.Clinic(), DomainTestData.Now, DomainTestData.Now.AddMinutes(minutes)));
    }

    [Fact]
    public void SlotStoresUtcAndDoesNotExposeMutableCollections()
    {
        var localStart = DomainTestData.Now.ToOffset(TimeSpan.FromHours(2));
        var slot = new TimeSlot(DomainTestData.Clinic(), localStart, localStart.AddMinutes(30));

        Assert.Equal(TimeSpan.Zero, slot.StartsAt.Offset);
        Assert.Equal(DomainTestData.Now, slot.StartsAt);
        var collection = Assert.IsAssignableFrom<ICollection<Appointment>>(slot.Appointments);
        Assert.True(collection.IsReadOnly);
        Assert.Throws<NotSupportedException>(() => collection.Add(DomainTestData.Appointment()));
    }

    [Fact]
    public void EntitiesExposeNoPublicSettersOrPublicParameterlessConstructors()
    {
        Type[] entityTypes = [typeof(Patient), typeof(Clinic), typeof(TimeSlot), typeof(Appointment)];
        foreach (var entityType in entityTypes)
        {
            Assert.Null(entityType.GetConstructor(Type.EmptyTypes));
            Assert.All(entityType.GetProperties(), property => Assert.False(property.SetMethod?.IsPublic == true));
        }
    }
}

