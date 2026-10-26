using PHCIS.Domain.Common;
using PHCIS.Domain.Exceptions;

namespace PHCIS.Domain.Entities;

public sealed class Appointment
{
    private byte[] _rowVersion = [];

    private Appointment() { }

    public Appointment(Patient patient, TimeSlot timeSlot, TimeProvider timeProvider, string reasonForVisit = "General consultation")
    {
        ArgumentNullException.ThrowIfNull(patient);
        ArgumentNullException.ThrowIfNull(timeSlot);
        ArgumentNullException.ThrowIfNull(timeProvider);
        var now = timeProvider.GetUtcNow();

        if (timeSlot.StartsAt <= now)
        {
            throw new DomainRuleException("Appointments must be scheduled before the slot starts.");
        }

        Id = Guid.NewGuid();
        Patient = patient;
        PatientId = patient.Id;
        TimeSlot = timeSlot;
        TimeSlotId = timeSlot.Id;
        Status = AppointmentStatus.Scheduled;
        CreatedAt = now.ToUniversalTime();
        ReasonForVisit = DomainGuard.RequiredText(reasonForVisit, 200, "Reason for visit");
    }

    public Guid Id { get; private set; }
    public Guid PatientId { get; private set; }
    public Patient Patient { get; private set; } = null!;
    public Guid TimeSlotId { get; private set; }
    public TimeSlot TimeSlot { get; private set; } = null!;
    public AppointmentStatus Status { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public string ReasonForVisit { get; private set; } = "General consultation";

    /// <summary>Moves a future scheduled appointment to a future slot at the same clinic.</summary>
    public void Reschedule(TimeSlot newSlot, TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(newSlot);
        var now = EnsureTransitionAllowed(timeProvider);
        if (now >= TimeSlot.StartsAt || now >= newSlot.StartsAt)
        {
            throw new DomainRuleException("Only future appointments and future slots can be rescheduled.");
        }
        if (newSlot.ClinicId != TimeSlot.ClinicId)
        {
            throw new DomainRuleException("Choose a replacement slot at the same clinic.");
        }
        TimeSlot = newSlot;
        TimeSlotId = newSlot.Id;
    }
    public DateTimeOffset? CancelledAt { get; private set; }
    public string? CancellationReason { get; private set; }
    public byte[] RowVersion => (byte[])_rowVersion.Clone();

    public void Cancel(string reason, TimeProvider timeProvider)
    {
        var now = EnsureTransitionAllowed(timeProvider);
        var normalizedReason = DomainGuard.RequiredText(reason, 500, "Cancellation reason");

        if (now >= TimeSlot.StartsAt)
        {
            throw new DomainRuleException("An appointment cannot be cancelled at or after its start.");
        }

        CancellationReason = normalizedReason;
        CancelledAt = now.ToUniversalTime();
        Status = AppointmentStatus.Cancelled;
    }

    public void Complete(TimeProvider timeProvider)
    {
        EnsureSlotHasEnded(timeProvider);
        Status = AppointmentStatus.Completed;
    }

    public void MarkNoShow(TimeProvider timeProvider)
    {
        EnsureSlotHasEnded(timeProvider);
        Status = AppointmentStatus.NoShow;
    }

    private DateTimeOffset EnsureTransitionAllowed(TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);

        if (Status != AppointmentStatus.Scheduled)
        {
            throw new DomainRuleException("Only scheduled appointments can change status.");
        }

        if (TimeSlot is null)
        {
            throw new DomainRuleException("Load the appointment's time slot before changing its status.");
        }

        var now = timeProvider.GetUtcNow();

        if (now < CreatedAt)
        {
            throw new DomainRuleException("A transition cannot occur before appointment creation.");
        }

        return now;
    }

    private void EnsureSlotHasEnded(TimeProvider timeProvider)
    {
        var now = EnsureTransitionAllowed(timeProvider);

        if (now < TimeSlot.EndsAt)
        {
            throw new DomainRuleException("Completion and no-show require the slot to have ended.");
        }
    }
}

