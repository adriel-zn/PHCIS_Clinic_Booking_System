using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PHCIS.Domain.Entities;
using PHCIS.Domain.Exceptions;
using PHCIS.Infrastructure.Persistence;

namespace PHCIS.UnitTests;

public sealed class PortalWorkflowTests
{
    [Fact]
    public void DomainReschedulingPreservesReferenceAndRejectsAnotherClinic()
    {
        var appointment = DomainTestData.Appointment();
        var id = appointment.Id;
        var replacement = new TimeSlot(appointment.TimeSlot.Clinic,
            appointment.TimeSlot.StartsAt.AddHours(2), appointment.TimeSlot.EndsAt.AddHours(2));
        appointment.Reschedule(replacement, new TestClock(DomainTestData.Now));
        Assert.Equal(id, appointment.Id);
        Assert.Equal(replacement.Id, appointment.TimeSlotId);
        Assert.Equal(AppointmentStatus.Scheduled, appointment.Status);

        var anotherClinic = new TimeSlot(DomainTestData.Clinic(), replacement.StartsAt, replacement.EndsAt);
        Assert.Throws<DomainRuleException>(() =>
            appointment.Reschedule(anotherClinic, new TestClock(DomainTestData.Now)));
        Assert.Equal(replacement.Id, appointment.TimeSlotId);
    }

    [Fact]
    public void DomainReschedulingRejectsTerminalAndStartedAppointments()
    {
        var cancelled = DomainTestData.Appointment();
        var replacement = new TimeSlot(cancelled.TimeSlot.Clinic,
            cancelled.TimeSlot.StartsAt.AddHours(2), cancelled.TimeSlot.EndsAt.AddHours(2));
        cancelled.Cancel("Cannot attend", new TestClock(DomainTestData.Now));
        Assert.Throws<DomainRuleException>(() =>
            cancelled.Reschedule(replacement, new TestClock(DomainTestData.Now)));

        var started = DomainTestData.Appointment();
        Assert.Throws<DomainRuleException>(() =>
            started.Reschedule(new TimeSlot(started.TimeSlot.Clinic, replacement.StartsAt, replacement.EndsAt),
                new TestClock(started.TimeSlot.StartsAt)));
    }

    [Fact]
    public async Task ReschedulingPreservesHistoryAndReleasesOriginalSlot()
    {
        await using var factory = new ApiFactory();
        await factory.SeedAsync();
        var alternate = await AlternateSlot(factory);
        using var client = factory.Client(factory.Token());
        var original = await ApiTests.Json(await client.PostAsJsonAsync("/api/appointments", new
        {
            timeSlotId = factory.SlotId,
            reasonForVisit = "Follow-up appointment"
        }));
        var id = original.GetProperty("id").GetGuid();

        var rescheduled = await client.PostAsJsonAsync($"/api/appointments/{id}/reschedule",
            new { timeSlotId = alternate });
        Assert.Equal(HttpStatusCode.OK, rescheduled.StatusCode);
        Assert.Equal(id, (await ApiTests.Json(rescheduled)).GetProperty("id").GetGuid());

        var appointments = await ApiTests.Json(await client.GetAsync("/api/appointments"));
        var entry = Assert.Single(appointments.EnumerateArray());
        Assert.Equal(alternate, entry.GetProperty("timeSlotId").GetGuid());
        Assert.Equal("Follow-up appointment", entry.GetProperty("reasonForVisit").GetString());

        var available = await ApiTests.Json(await client.GetAsync(ApiTests.AvailabilityUrl(factory.ClinicId)));
        Assert.Contains(available.GetProperty("items").EnumerateArray(),
            slot => slot.GetProperty("id").GetGuid() == factory.SlotId);
    }

    [Fact]
    public async Task ReschedulingRejectsOccupiedSlotsAndOtherPatients()
    {
        await using var factory = new ApiFactory();
        await factory.SeedAsync();
        var alternate = await AlternateSlot(factory);
        using var client = factory.Client(factory.Token());
        var original = await ApiTests.Json(await client.PostAsJsonAsync("/api/appointments",
            new { timeSlotId = factory.SlotId }));
        var id = original.GetProperty("id").GetGuid();
        await client.PostAsJsonAsync("/api/appointments", new { timeSlotId = alternate });

        await ApiTests.AssertProblem(await client.PostAsJsonAsync($"/api/appointments/{id}/reschedule",
            new { timeSlotId = alternate }), HttpStatusCode.Conflict);

        using var other = factory.Client(factory.Token(Guid.NewGuid().ToString()));
        await ApiTests.AssertProblem(await other.PostAsJsonAsync($"/api/appointments/{id}/reschedule",
            new { timeSlotId = alternate }), HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task ProfileChangesAreScopedAndPersisted()
    {
        await using var factory = new ApiFactory();
        await factory.SeedAsync();
        using var client = factory.Client(factory.Token());

        var updated = await client.PostAsJsonAsync("/api/account/profile", new
        {
            firstName = "Updated",
            lastName = "Patient",
            phoneNumber = "082 555 0199",
            dateOfBirth = "1990-01-02",
            nationalId = "DEV-EXAMPLE"
        });

        Assert.Equal(HttpStatusCode.OK, updated.StatusCode);
        var profile = await ApiTests.Json(await client.GetAsync("/api/account/me"));
        Assert.Equal(factory.PatientId, profile.GetProperty("id").GetGuid());
        Assert.Equal("Updated", profile.GetProperty("firstName").GetString());
        Assert.Equal("DEV-EXAMPLE", profile.GetProperty("nationalId").GetString());

        using var other = factory.Client(factory.Token(Guid.NewGuid().ToString()));
        Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync("/api/account/me")).StatusCode);
    }

    private static async Task<Guid> AlternateSlot(ApiFactory factory)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<PhcisDbContext>();
        var existing = await context.TimeSlots.Include(x => x.Clinic).SingleAsync();
        var slot = new TimeSlot(existing.Clinic, existing.StartsAt.AddHours(2), existing.EndsAt.AddHours(2));
        context.TimeSlots.Add(slot);
        await context.SaveChangesAsync();
        return slot.Id;
    }
}

