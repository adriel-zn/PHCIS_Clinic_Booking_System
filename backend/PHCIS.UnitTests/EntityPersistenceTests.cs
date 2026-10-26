using Microsoft.EntityFrameworkCore;
using PHCIS.Domain.Entities;
using PHCIS.Domain.Exceptions;
using PHCIS.Infrastructure.Persistence;

namespace PHCIS.UnitTests;

/// <summary>Checks EF construction and relationship fixup, not SQL Server constraint execution.</summary>
public sealed class EntityPersistenceTests
{
    [Fact]
    public async Task EncapsulatedGraphCanBeSavedReloadedAndCancelled()
    {
        var options = new DbContextOptionsBuilder<PhcisDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var original = DomainTestData.Appointment();

        await using (var context = new PhcisDbContext(options))
        {
            context.Add(original);
            await context.SaveChangesAsync();
        }

        await using (var context = new PhcisDbContext(options))
        {
            var appointment = await context.Appointments
                .Include(x => x.Patient)
                .Include(x => x.TimeSlot)
                .ThenInclude(x => x.Clinic)
                .SingleAsync();

            Assert.Equal(original.Id, appointment.Id);
            Assert.Single(appointment.Patient.Appointments);
            Assert.Single(appointment.TimeSlot.Appointments);
            Assert.Single(appointment.TimeSlot.Clinic.TimeSlots);

            appointment.Cancel("Cannot attend", new TestClock(DomainTestData.Now));
            await context.SaveChangesAsync();
        }

        await using (var context = new PhcisDbContext(options))
        {
            var appointment = await context.Appointments.SingleAsync();
            Assert.Equal(AppointmentStatus.Cancelled, appointment.Status);
            Assert.Equal("Cannot attend", appointment.CancellationReason);
            Assert.Equal(DomainTestData.Now, appointment.CancelledAt);
        }
    }

    [Fact]
    public async Task TransitionRequiresLoadedSlotAndProtectsConcurrencyToken()
    {
        var options = new DbContextOptionsBuilder<PhcisDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var original = DomainTestData.Appointment();

        await using (var context = new PhcisDbContext(options))
        {
            context.Add(original);
            await context.SaveChangesAsync();
        }

        await using (var context = new PhcisDbContext(options))
        {
            var appointment = await context.Appointments.SingleAsync();
            Assert.Throws<DomainRuleException>(() =>
                appointment.Cancel("Cannot attend", new TestClock(DomainTestData.Now)));

            // EF can set the private field, but callers only receive a copy.
            context.Entry(appointment).Property(x => x.RowVersion).CurrentValue = [1, 2, 3];
            var copy = appointment.RowVersion;
            copy[0] = 99;
            Assert.Equal((byte)1, appointment.RowVersion[0]);

            await context.Entry(appointment).Reference(x => x.TimeSlot).LoadAsync();
            appointment.Cancel("Cannot attend", new TestClock(DomainTestData.Now));
            Assert.Equal(AppointmentStatus.Cancelled, appointment.Status);
        }
    }
}

