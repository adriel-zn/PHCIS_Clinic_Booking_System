using PHCIS.Domain.Entities;
using PHCIS.Domain.Exceptions;

namespace PHCIS.UnitTests;

public sealed class AppointmentTests
{
    [Fact]
    public void CreationStartsScheduledWithRelatedIdsAndUtcTimestamp()
    {
        var patient = DomainTestData.Patient();
        var slot = DomainTestData.Slot();
        var appointment = new Appointment(patient, slot, new TestClock(DomainTestData.Now));

        Assert.NotEqual(Guid.Empty, appointment.Id);
        Assert.Equal(patient.Id, appointment.PatientId);
        Assert.Equal(slot.Id, appointment.TimeSlotId);
        Assert.Equal(AppointmentStatus.Scheduled, appointment.Status);
        Assert.Equal(DomainTestData.Now, appointment.CreatedAt);
        Assert.Null(appointment.CancellationReason);
        Assert.Null(appointment.CancelledAt);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public void CreationRejectsSlotsAlreadyStarted(int minutesAfterStart)
    {
        var slot = DomainTestData.Slot();
        var clock = new TestClock(slot.StartsAt.AddMinutes(minutesAfterStart));

        Assert.Throws<DomainRuleException>(() => new Appointment(DomainTestData.Patient(), slot, clock));
    }

    [Fact]
    public void CancellationBeforeStartRecordsTrimmedReasonAndTimestamp()
    {
        var appointment = DomainTestData.Appointment();
        var now = appointment.TimeSlot.StartsAt.AddTicks(-1);

        appointment.Cancel("  Patient requested cancellation  ", new TestClock(now));

        Assert.Equal(AppointmentStatus.Cancelled, appointment.Status);
        Assert.Equal("Patient requested cancellation", appointment.CancellationReason);
        Assert.Equal(now, appointment.CancelledAt);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public void CancellationAtOrAfterStartIsRejectedWithoutChangingState(int ticksAfterStart)
    {
        var appointment = DomainTestData.Appointment();
        var clock = new TestClock(appointment.TimeSlot.StartsAt.AddTicks(ticksAfterStart));

        Assert.Throws<DomainRuleException>(() => appointment.Cancel("Cannot attend", clock));
        AssertScheduled(appointment);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void CancellationRequiresReason(string? reason)
    {
        var appointment = DomainTestData.Appointment();

        Assert.Throws<DomainRuleException>(() =>
            appointment.Cancel(reason!, new TestClock(DomainTestData.Now)));
        AssertScheduled(appointment);
    }

    [Fact]
    public void CancellationRejectsOversizedReasonAndAcceptsMaximumLength()
    {
        var appointment = DomainTestData.Appointment();
        var clock = new TestClock(DomainTestData.Now);

        Assert.Throws<DomainRuleException>(() => appointment.Cancel(new string('x', 501), clock));
        AssertScheduled(appointment);
        appointment.Cancel(new string('x', 500), clock);
        Assert.Equal(500, appointment.CancellationReason!.Length);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CompletionAndNoShowAreAllowedAtSlotEnd(bool noShow)
    {
        var appointment = DomainTestData.Appointment();
        var clock = new TestClock(appointment.TimeSlot.EndsAt);

        if (noShow)
        {
            appointment.MarkNoShow(clock);
        }
        else
        {
            appointment.Complete(clock);
        }

        Assert.Equal(noShow ? AppointmentStatus.NoShow : AppointmentStatus.Completed, appointment.Status);
        Assert.Null(appointment.CancelledAt);
        Assert.Null(appointment.CancellationReason);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CompletionAndNoShowAreRejectedBeforeSlotEnd(bool noShow)
    {
        var appointment = DomainTestData.Appointment();
        var clock = new TestClock(appointment.TimeSlot.EndsAt.AddTicks(-1));

        Assert.Throws<DomainRuleException>(() =>
        {
            if (noShow)
            {
                appointment.MarkNoShow(clock);
            }
            else
            {
                appointment.Complete(clock);
            }
        });
        AssertScheduled(appointment);
    }

    [Theory]
    [InlineData(AppointmentStatus.Cancelled)]
    [InlineData(AppointmentStatus.Completed)]
    [InlineData(AppointmentStatus.NoShow)]
    public void EveryTerminalStateRejectsEveryFurtherTransition(AppointmentStatus terminalState)
    {
        var appointment = DomainTestData.Appointment();
        var ended = new TestClock(appointment.TimeSlot.EndsAt);

        switch (terminalState)
        {
            case AppointmentStatus.Cancelled:
                appointment.Cancel("Cannot attend", new TestClock(DomainTestData.Now));
                break;
            case AppointmentStatus.Completed:
                appointment.Complete(ended);
                break;
            case AppointmentStatus.NoShow:
                appointment.MarkNoShow(ended);
                break;
        }

        var reason = appointment.CancellationReason;
        var cancelledAt = appointment.CancelledAt;
        Assert.Throws<DomainRuleException>(() => appointment.Cancel("Changed", ended));
        Assert.Throws<DomainRuleException>(() => appointment.Complete(ended));
        Assert.Throws<DomainRuleException>(() => appointment.MarkNoShow(ended));
        Assert.Equal(terminalState, appointment.Status);
        Assert.Equal(reason, appointment.CancellationReason);
        Assert.Equal(cancelledAt, appointment.CancelledAt);
    }

    [Fact]
    public void CancellationCannotPredateCreation()
    {
        var appointment = DomainTestData.Appointment();

        Assert.Throws<DomainRuleException>(() =>
            appointment.Cancel("Cannot attend", new TestClock(appointment.CreatedAt.AddTicks(-1))));
        AssertScheduled(appointment);
    }

    private static void AssertScheduled(Appointment appointment)
    {
        Assert.Equal(AppointmentStatus.Scheduled, appointment.Status);
        Assert.Null(appointment.CancelledAt);
        Assert.Null(appointment.CancellationReason);
    }
}

