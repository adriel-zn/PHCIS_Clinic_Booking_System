using PHCIS.Web.Models;
using PHCIS.Web.Services;

namespace PHCIS.Web.Tests;

internal sealed class FakeAppointmentApiClient : IAppointmentApiClient
{
    public static readonly DateTimeOffset Now = new(2026, 10, 26, 8, 0, 0, TimeSpan.Zero);
    public static readonly Guid ClinicId = Guid.NewGuid();
    public static readonly SlotSummary Slot = new(Guid.NewGuid(), ClinicId, Now.AddHours(1), Now.AddHours(1.5));
    public int Searches { get; private set; }
    public int Bookings { get; private set; }
    public int Cancellations { get; private set; }
    public AvailabilityQuery? LastQuery { get; private set; }
    public (Guid Id, string Reason)? LastCancellation { get; private set; }
    public AvailableSlotsPage Page { get; set; } = new([Slot], 0, 12, false);
    public Exception? Failure { get; set; }
    public TaskCompletionSource<AppointmentSummary>? PendingBooking { get; set; }
    public TaskCompletionSource<AvailableSlotsPage>? PendingSearch { get; set; }

    public Task<AvailableSlotsPage> FindAvailableAsync(AvailabilityQuery query, CancellationToken cancellationToken)
    {
        Searches++;
        LastQuery = query;
        if (Failure is not null)
        {
            throw Failure;
        }
        return PendingSearch?.Task.WaitAsync(cancellationToken) ?? Task.FromResult(Page);
    }

    public Task<AppointmentSummary> BookAsync(Guid slotId, CancellationToken cancellationToken, string reasonForVisit = "General consultation")
    {
        Bookings++;
        if (Failure is not null)
        {
            throw Failure;
        }
        return PendingBooking?.Task.WaitAsync(cancellationToken) ?? Task.FromResult(Appointment());
    }

    public Task<AppointmentSummary> CancelAsync(Guid appointmentId, string reason, CancellationToken cancellationToken)
    {
        Cancellations++;
        LastCancellation = (appointmentId, reason);
        if (Failure is not null)
        {
            throw Failure;
        }
        return Task.FromResult(Appointment() with
        {
            Id = appointmentId,
            Status = "Cancelled",
            CancelledAt = Now,
            CancellationReason = reason.Trim()
        });
    }

    public Task<AppointmentSummary> RescheduleAsync(Guid appointmentId, Guid slotId, CancellationToken cancellationToken) =>
        Task.FromResult(Appointment() with { Id = appointmentId, TimeSlotId = slotId });

    public static AppointmentSummary Appointment() =>
        new(Guid.NewGuid(), Guid.NewGuid(), Slot.Id, "Scheduled", Now, null, null);
}

internal sealed class FixedClock : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => FakeAppointmentApiClient.Now;
}

