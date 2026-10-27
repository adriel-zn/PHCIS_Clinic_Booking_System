using FluentValidation;
using PHCIS.Application.Exceptions;
using PHCIS.Application.Models;
using PHCIS.Domain.Entities;
using PHCIS.Domain.Exceptions;

namespace PHCIS.UnitTests;

public sealed class AppointmentServiceTests
{
    [Fact]
    public async Task BookingPersistsScheduledAppointmentAndMapsResponse()
    {
        using var fixture = new ApplicationServiceFixture();
        var patient = DomainTestData.Patient();
        var slot = DomainTestData.Slot();
        fixture.Store.Patients.Add(patient);
        fixture.Store.Slots.Add(slot);
        using var source = new CancellationTokenSource();

        var result = await fixture.Appointments().BookAsync(
            new BookAppointmentRequest(patient.Id, slot.Id), source.Token);

        Assert.Equal(patient.Id, result.PatientId);
        Assert.Equal(slot.Id, result.TimeSlotId);
        Assert.Equal(AppointmentStatus.Scheduled, result.Status);
        Assert.Equal(DomainTestData.Now, result.CreatedAt);
        Assert.Equal(result.Id, Assert.Single(fixture.Store.Appointments).Id);
        Assert.Equal(1, fixture.Store.Saves);
        Assert.Equal(source.Token, fixture.Store.LastToken);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task BookingRejectsEmptyIdentifiersBeforeReading(bool emptyPatient)
    {
        using var fixture = new ApplicationServiceFixture();
        var request = new BookAppointmentRequest(
            emptyPatient ? Guid.Empty : Guid.NewGuid(),
            emptyPatient ? Guid.NewGuid() : Guid.Empty);

        await Assert.ThrowsAsync<ValidationException>(() => fixture.Appointments().BookAsync(request));

        Assert.Equal(0, fixture.Store.Reads);
        Assert.Equal(0, fixture.Store.Saves);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task BookingRejectsMissingPatientOrSlot(bool missingPatient)
    {
        using var fixture = new ApplicationServiceFixture();
        var patient = DomainTestData.Patient();
        var slot = DomainTestData.Slot();
        if (!missingPatient)
        {
            fixture.Store.Patients.Add(patient);
        }

        await Assert.ThrowsAsync<ResourceNotFoundException>(() =>
            fixture.Appointments().BookAsync(new BookAppointmentRequest(patient.Id, slot.Id)));

        Assert.Null(fixture.Store.Pending);
        Assert.Equal(0, fixture.Store.Saves);
    }

    [Theory]
    [InlineData(AppointmentStatus.Scheduled)]
    [InlineData(AppointmentStatus.Completed)]
    [InlineData(AppointmentStatus.NoShow)]
    public async Task BookingRejectsEveryNonCancelledOccupant(AppointmentStatus status)
    {
        using var fixture = new ApplicationServiceFixture();
        var existing = fixture.SeedAppointment();
        var ended = new TestClock(existing.TimeSlot.EndsAt);
        if (status == AppointmentStatus.Completed)
        {
            existing.Complete(ended);
        }
        else if (status == AppointmentStatus.NoShow)
        {
            existing.MarkNoShow(ended);
        }

        await Assert.ThrowsAsync<AppointmentConflictException>(() => fixture.Appointments().BookAsync(
            new BookAppointmentRequest(existing.PatientId, existing.TimeSlotId)));

        Assert.Null(fixture.Store.Pending);
        Assert.Equal(0, fixture.Store.Saves);
    }

    [Fact]
    public async Task BookingCanReuseCancelledSlotWhileRetainingHistory()
    {
        using var fixture = new ApplicationServiceFixture();
        var previous = fixture.SeedAppointment();
        previous.Cancel("Cannot attend", fixture.Clock);

        var result = await fixture.Appointments().BookAsync(
            new BookAppointmentRequest(previous.PatientId, previous.TimeSlotId));

        Assert.NotEqual(previous.Id, result.Id);
        Assert.Equal(2, fixture.Store.Appointments.Count);
        Assert.Equal(AppointmentStatus.Cancelled, previous.Status);
    }

    [Fact]
    public async Task BookingRejectsStartedSlotThroughDomainWithoutSaving()
    {
        using var fixture = new ApplicationServiceFixture();
        var patient = DomainTestData.Patient();
        var slot = DomainTestData.Slot();
        fixture.Store.Patients.Add(patient);
        fixture.Store.Slots.Add(slot);
        fixture.Clock = new TestClock(slot.StartsAt);

        await Assert.ThrowsAsync<DomainRuleException>(() => fixture.Appointments().BookAsync(
            new BookAppointmentRequest(patient.Id, slot.Id)));

        Assert.Null(fixture.Store.Pending);
        Assert.Equal(0, fixture.Store.Saves);
    }

    [Fact]
    public async Task SaveConflictIsPropagatedWithoutReturningSuccessfulBooking()
    {
        using var fixture = new ApplicationServiceFixture();
        var patient = DomainTestData.Patient();
        var slot = DomainTestData.Slot();
        fixture.Store.Patients.Add(patient);
        fixture.Store.Slots.Add(slot);
        var conflict = new AppointmentConflictException("Concurrent booking.");
        fixture.Store.SaveFailure = conflict;

        var exception = await Assert.ThrowsAsync<AppointmentConflictException>(() =>
            fixture.Appointments().BookAsync(new BookAppointmentRequest(patient.Id, slot.Id)));

        Assert.Same(conflict, exception);
        Assert.Empty(fixture.Store.Appointments);
    }

    [Fact]
    public async Task CancellationSavesDomainMetadataAndMapsResponse()
    {
        using var fixture = new ApplicationServiceFixture();
        var appointment = fixture.SeedAppointment();
        using var source = new CancellationTokenSource();

        var result = await fixture.Appointments().CancelAsync(
            new CancelAppointmentRequest(appointment.Id, appointment.PatientId, " Cannot attend "),
            source.Token);

        Assert.Equal(AppointmentStatus.Cancelled, result.Status);
        Assert.Equal("Cannot attend", result.CancellationReason);
        Assert.Equal(DomainTestData.Now, result.CancelledAt);
        Assert.Equal(1, fixture.Store.Saves);
        Assert.Equal(source.Token, fixture.Store.LastToken);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CancellationDoesNotExposeMissingOrOtherPatientsAppointment(bool missing)
    {
        using var fixture = new ApplicationServiceFixture();
        var appointment = fixture.SeedAppointment();
        var request = new CancelAppointmentRequest(
            missing ? Guid.NewGuid() : appointment.Id,
            missing ? appointment.PatientId : Guid.NewGuid(),
            "Cannot attend");

        await Assert.ThrowsAsync<ResourceNotFoundException>(() => fixture.Appointments().CancelAsync(request));

        Assert.Equal(AppointmentStatus.Scheduled, appointment.Status);
        Assert.Equal(0, fixture.Store.Saves);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(null)]
    public async Task CancellationRejectsMissingReasonBeforeReading(string? reason)
    {
        using var fixture = new ApplicationServiceFixture();

        await Assert.ThrowsAsync<ValidationException>(() => fixture.Appointments().CancelAsync(
            new CancelAppointmentRequest(Guid.NewGuid(), Guid.NewGuid(), reason!)));

        Assert.Equal(0, fixture.Store.Reads);
        Assert.Equal(0, fixture.Store.Saves);
    }

    [Fact]
    public async Task CancellationEnforcesDomainTimeAndTerminalStateRules()
    {
        using var fixture = new ApplicationServiceFixture();
        var appointment = fixture.SeedAppointment();
        var request = new CancelAppointmentRequest(appointment.Id, appointment.PatientId, "Cannot attend");
        fixture.Clock = new TestClock(appointment.TimeSlot.StartsAt);

        await Assert.ThrowsAsync<DomainRuleException>(() => fixture.Appointments().CancelAsync(request));
        Assert.Equal(0, fixture.Store.Saves);

        fixture.Clock = new TestClock(DomainTestData.Now);
        await fixture.Appointments().CancelAsync(request);
        await Assert.ThrowsAsync<DomainRuleException>(() => fixture.Appointments().CancelAsync(request));
        Assert.Equal(1, fixture.Store.Saves);
    }

    [Fact]
    public async Task CancellationPropagatesSaveConflict()
    {
        using var fixture = new ApplicationServiceFixture();
        var appointment = fixture.SeedAppointment();
        fixture.Store.SaveFailure = new AppointmentConflictException("Concurrent change.");

        await Assert.ThrowsAsync<AppointmentConflictException>(() => fixture.Appointments().CancelAsync(
            new CancelAppointmentRequest(appointment.Id, appointment.PatientId, "Cannot attend")));
    }

    [Fact]
    public async Task CancelledTokenPreventsAllRepositoryCalls()
    {
        using var fixture = new ApplicationServiceFixture();
        using var source = new CancellationTokenSource();
        source.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => fixture.Appointments().BookAsync(
            new BookAppointmentRequest(Guid.NewGuid(), Guid.NewGuid()), source.Token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => fixture.Appointments().CancelAsync(
            new CancelAppointmentRequest(Guid.NewGuid(), Guid.NewGuid(), "Cannot attend"), source.Token));

        Assert.Equal(0, fixture.Store.Reads);
        Assert.Equal(0, fixture.Store.Saves);
    }
}

