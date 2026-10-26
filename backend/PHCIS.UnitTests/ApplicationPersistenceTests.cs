using Microsoft.EntityFrameworkCore;
using PHCIS.Application.Exceptions;
using PHCIS.Application.Models;
using PHCIS.Application.Services;
using PHCIS.Application.Validation;
using PHCIS.Domain.Entities;
using PHCIS.Infrastructure.Persistence;
using PHCIS.Infrastructure.Persistence.Repositories;

namespace PHCIS.UnitTests;

public sealed class ApplicationPersistenceTests
{
    [Fact]
    public async Task AvailabilityFiltersClinicWindowFutureAndEveryOccupyingStatusThenPagesStably()
    {
        var options = Options();
        var clinic = DomainTestData.Clinic();
        TimeSlot Slot(int hours, int minutes = 30) =>
            new(clinic, DomainTestData.Now.AddHours(hours), DomainTestData.Now.AddHours(hours).AddMinutes(minutes));

        var past = Slot(-1);
        var startingNow = Slot(0);
        var cancelledSlot = Slot(1);
        var occupiedSlot = Slot(2);
        var completedSlot = Slot(3);
        var noShowSlot = Slot(4);
        var openSlot = Slot(5);
        var tiedSlot = Slot(5, 15);
        var upperBoundary = Slot(6);
        var otherClinic = DomainTestData.Slot();
        var patient = DomainTestData.Patient();
        var bookingClock = new TestClock(DomainTestData.Now.AddDays(-1));
        var cancelled = new Appointment(patient, cancelledSlot, bookingClock);
        cancelled.Cancel("Cannot attend", new TestClock(DomainTestData.Now));
        var occupied = new Appointment(patient, occupiedSlot, bookingClock);
        var completed = new Appointment(patient, completedSlot, bookingClock);
        completed.Complete(new TestClock(completedSlot.EndsAt));
        var noShow = new Appointment(patient, noShowSlot, bookingClock);
        noShow.MarkNoShow(new TestClock(noShowSlot.EndsAt));

        await using (var context = new PhcisDbContext(options))
        {
            context.AddRange(past, startingNow, cancelledSlot, occupiedSlot, completedSlot,
                noShowSlot, openSlot, tiedSlot, upperBoundary, otherClinic);
            context.AddRange(cancelled, occupied, completed, noShow);
            await context.SaveChangesAsync();
        }

        await using (var context = new PhcisDbContext(options))
        {
            var repository = new TimeSlotRepository(context);
            var rows = await repository.FindAvailableAsync(
                clinic.Id, DomainTestData.Now.AddDays(-1), upperBoundary.StartsAt,
                DomainTestData.Now, 0, 100, default);
            var expected = new[] { cancelledSlot, openSlot, tiedSlot }
                .OrderBy(x => x.StartsAt).ThenBy(x => x.Id).Select(x => x.Id).ToArray();

            Assert.Equal(expected, rows.Select(x => x.Id));
            Assert.Empty(context.ChangeTracker.Entries());

            var page = await repository.FindAvailableAsync(
                clinic.Id, DomainTestData.Now.AddDays(-1), upperBoundary.StartsAt,
                DomainTestData.Now, 1, 1, default);
            Assert.Equal(expected[1], Assert.Single(page).Id);
        }
    }

    [Fact]
    public async Task RealRepositoriesSupportBookCancelAndReuseAcrossFreshScopes()
    {
        var options = Options();
        var patient = DomainTestData.Patient();
        var slot = DomainTestData.Slot();
        await using (var context = new PhcisDbContext(options))
        {
            context.AddRange(patient, slot);
            await context.SaveChangesAsync();
        }

        using var fixture = new ApplicationServiceFixture();
        AppointmentService Service(PhcisDbContext context) => new(
            new PatientReader(context),
            new TimeSlotRepository(context),
            new AppointmentRepository(context),
            new UnitOfWork(context),
            new BookAppointmentRequestValidator(),
            new CancelAppointmentRequestValidator(),
            new RescheduleAppointmentRequestValidator(),
            fixture.Mapper,
            fixture.Clock);

        Guid originalId;
        await using (var context = new PhcisDbContext(options))
        {
            var booked = await Service(context).BookAsync(new BookAppointmentRequest(patient.Id, slot.Id));
            originalId = booked.Id;
        }

        await using (var context = new PhcisDbContext(options))
        {
            await Service(context).CancelAsync(new CancelAppointmentRequest(originalId, patient.Id, "Cannot attend"));
        }

        await using (var context = new PhcisDbContext(options))
        {
            var repository = new AppointmentRepository(context);
            Assert.False(await repository.IsSlotOccupiedAsync(slot.Id, default));
            var replacement = await Service(context).BookAsync(new BookAppointmentRequest(patient.Id, slot.Id));
            Assert.NotEqual(originalId, replacement.Id);
            Assert.Equal(2, await context.Appointments.CountAsync());
            Assert.Equal(1, await context.Patients.CountAsync());
            Assert.Equal(1, await context.TimeSlots.CountAsync());
        }
    }

    [Fact]
    public async Task UnitOfWorkTranslatesConcurrentDeletionAndDetachesFailedWrite()
    {
        var options = Options();
        var appointment = DomainTestData.Appointment();
        await using (var context = new PhcisDbContext(options))
        {
            context.Add(appointment);
            await context.SaveChangesAsync();
        }

        await using var first = new PhcisDbContext(options);
        var loaded = await new AppointmentRepository(first).FindWithSlotAsync(appointment.Id, default);
        await using (var second = new PhcisDbContext(options))
        {
            var deleted = await second.Appointments.SingleAsync();
            second.Remove(deleted);
            await second.SaveChangesAsync();
        }

        loaded!.Cancel("Cannot attend", new TestClock(DomainTestData.Now));
        await Assert.ThrowsAsync<AppointmentConflictException>(() => new UnitOfWork(first).SaveChangesAsync(default));
        Assert.Equal(EntityState.Detached, first.Entry(loaded).State);
        Assert.Equal(0, await first.SaveChangesAsync());
    }

    private static DbContextOptions<PhcisDbContext> Options() =>
        new DbContextOptionsBuilder<PhcisDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
}

