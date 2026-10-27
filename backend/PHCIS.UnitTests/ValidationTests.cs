using PHCIS.Application.Validation;

namespace PHCIS.UnitTests;

public sealed class ValidationTests
{
    [Fact]
    public void DomainCreatedEntitiesPassBasicApplicationValidation()
    {
        Assert.True(new PatientValidator(new TestClock(DomainTestData.Now)).Validate(DomainTestData.Patient()).IsValid);

        Assert.True(new ClinicValidator().Validate(DomainTestData.Clinic()).IsValid);

        Assert.True(new TimeSlotValidator().Validate(DomainTestData.Slot()).IsValid);

        Assert.True(new AppointmentValidator().Validate(DomainTestData.Appointment()).IsValid);
    }
}

