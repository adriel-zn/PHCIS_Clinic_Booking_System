using System.ComponentModel.DataAnnotations;
using PHCIS.Web.Models;

namespace PHCIS.Web.Tests;

public sealed class FormValidationTests
{
    [Fact]
    public void SearchRejectsEmptyClinicReferenceAndPastDate()
    {
        Assert.False(IsValid(new ClinicSearchModel { ClinicReference = Guid.Empty.ToString(), Date = new DateOnly(2026, 10, 26) }));
        Assert.False(IsValid(new ClinicSearchModel
        {
            ClinicReference = Guid.NewGuid().ToString(),
            Date = new DateOnly(2026, 10, 25),
            EarliestDate = new DateOnly(2026, 10, 26)
        }));
    }

    [Fact]
    public void SearchRejectsLastRepresentableDayRatherThanOverflowingDayRange()
    {
        Assert.False(IsValid(new ClinicSearchModel { ClinicReference = Guid.NewGuid().ToString(), Date = DateOnly.MaxValue }));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CancellationChecksConfirmationAndReasonLength(bool confirmed)
    {
        Assert.False(IsValid(new CancellationModel
        {
            AppointmentReference = Guid.NewGuid().ToString(),
            Reason = new string('x', 501),
            Confirmed = confirmed
        }));
        Assert.Equal(confirmed, IsValid(new CancellationModel
        {
            AppointmentReference = Guid.NewGuid().ToString(),
            Reason = " Cannot attend ",
            Confirmed = confirmed
        }));
    }

    private static bool IsValid(object model) =>
        Validator.TryValidateObject(model, new ValidationContext(model), new List<ValidationResult>(), true);
}

