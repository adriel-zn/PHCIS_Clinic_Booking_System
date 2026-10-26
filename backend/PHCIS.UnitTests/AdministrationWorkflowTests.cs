using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PHCIS.Domain.Entities;
using PHCIS.Domain.Exceptions;
using PHCIS.Infrastructure.Identity;
using PHCIS.Infrastructure.Persistence;

namespace PHCIS.UnitTests;

public sealed class AdministrationWorkflowTests
{
    [Fact]
    public async Task AdministratorEditsClinicWithoutChangingItsAppointments()
    {
        await using var factory = new ApiFactory(clinicAdministrator: true);
        await factory.SeedAsync();
        using var client = factory.Client(factory.Token());
        Assert.Equal(HttpStatusCode.Created, (await client.PostAsJsonAsync("/api/appointments", new { timeSlotId = factory.SlotId })).StatusCode);
        var result = await client.PostAsJsonAsync($"/api/clinic-management/{factory.ClinicId}/details",
            new { name = "Renamed clinic", address = "New address", phoneNumber = "031 555 0101" });
        Assert.Equal(HttpStatusCode.NoContent, result.StatusCode);
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<PhcisDbContext>();
        Assert.Equal("Renamed clinic", (await context.Clinics.SingleAsync()).Name);
        Assert.Equal(factory.SlotId, (await context.Appointments.SingleAsync()).TimeSlotId);
    }

    [Fact]
    public async Task SlotManagementRespectsAvailabilityOverlapAndAppointmentHistory()
    {
        await using var factory = new ApiFactory(clinicAdministrator: true);
        await factory.SeedAsync();
        using var client = factory.Client(factory.Token());
        var path = $"/api/clinic-management/{factory.ClinicId}/slots";
        var start = DomainTestData.Now.AddDays(6);
        var result = await client.PostAsJsonAsync(path, new { startsAt = start, endsAt = start.AddMinutes(30) });
        Assert.Equal(HttpStatusCode.Created, result.StatusCode);
        var id = (await ApiTests.Json(result)).GetProperty("id").GetGuid();
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync(path,
            new { startsAt = start.AddMinutes(15), endsAt = start.AddMinutes(45) })).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsJsonAsync($"{path}/{id}/schedule",
            new { startsAt = start.AddHours(1), endsAt = start.AddHours(1.5) })).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsJsonAsync($"{path}/{id}/availability", new { isBlocked = true })).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync("/api/appointments", new { timeSlotId = id })).StatusCode);
        var range = $"from={Uri.EscapeDataString(start.ToString("O"))}&to={Uri.EscapeDataString(start.AddDays(1).ToString("O"))}";
        var available = await ApiTests.Json(await client.GetAsync($"/api/clinics/{factory.ClinicId}/available-slots?{range}"));
        Assert.DoesNotContain(id.ToString(), available.GetRawText());
        var managed = await ApiTests.Json(await client.GetAsync($"{path}?{range}"));
        Assert.True(managed[0].GetProperty("isBlocked").GetBoolean());
        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsJsonAsync($"{path}/{id}/availability", new { isBlocked = false })).StatusCode);
        var booking = await client.PostAsJsonAsync("/api/appointments", new { timeSlotId = id });
        Assert.Equal(HttpStatusCode.Created, booking.StatusCode);
        var appointmentId = (await ApiTests.Json(booking)).GetProperty("id").GetGuid();
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync($"{path}/{id}/availability", new { isBlocked = true })).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync($"{path}/{id}/schedule",
            new { startsAt = start.AddHours(2), endsAt = start.AddHours(2.5) })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync($"/api/appointments/{appointmentId}/cancellation", new { reason = "Test cancellation" })).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync($"{path}/{id}/schedule",
            new { startsAt = start.AddHours(2), endsAt = start.AddHours(2.5) })).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsJsonAsync($"{path}/{id}/availability", new { isBlocked = true })).StatusCode);
    }

    [Theory]
    [InlineData("details")]
    [InlineData("slots")]
    public async Task PatientsCannotWriteManagementEndpoints(string endpoint)
    {
        await using var factory = new ApiFactory();
        using var client = factory.Client(factory.Token());
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync(
            $"/api/clinic-management/{factory.ClinicId}/{endpoint}", new { })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync("/api/administrators",
            new { email = "target@example.test", confirmAdministratorAccess = true })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync(
            $"/api/clinic-management/{factory.ClinicId}/slots?from=2026-10-26&to=2026-10-27")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync(
            $"/api/clinic-management/{factory.ClinicId}/slots/{factory.SlotId}/availability", new { isBlocked = true })).StatusCode);
    }

    [Fact]
    public async Task GrantingAdministratorRequiresConfirmationAndTakesEffectAtNextLogin()
    {
        await using var factory = new ApiFactory(clinicAdministrator: true);
        using var target = factory.Client();
        Assert.Equal(HttpStatusCode.Created, (await target.PostAsJsonAsync("/api/account/register", new
        {
            firstName = "Second",
            lastName = "Administrator",
            email = "second@example.test",
            password = "SecondAdmin!2026",
            phoneNumber = "082 555 0100",
            dateOfBirth = "1990-01-01"
        })).StatusCode);
        using var admin = factory.Client(factory.Token());
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync("/api/administrators",
            new { email = "second@example.test", confirmAdministratorAccess = false })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await admin.PostAsJsonAsync("/api/administrators",
            new { email = "missing@example.test", confirmAdministratorAccess = true })).StatusCode);
        for (var attempt = 0; attempt < 2; attempt++)
            Assert.Equal(HttpStatusCode.NoContent, (await admin.PostAsJsonAsync("/api/administrators",
                new { email = "second@example.test", confirmAdministratorAccess = true })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await target.PostAsJsonAsync("/api/account/login",
            new { email = "second@example.test", password = "SecondAdmin!2026", rememberMe = false })).StatusCode);
        Assert.True((await ApiTests.Json(await target.GetAsync("/api/clinic-management/access"))).GetProperty("canManageClinics").GetBoolean());
        using var scope = factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<PortalUser>>();
        var user = await users.FindByEmailAsync("second@example.test");
        Assert.True(await users.IsInRoleAsync(user!, PortalRoles.Admin));
        Assert.True(await users.IsInRoleAsync(user!, PortalRoles.Patient));
    }

    [Fact]
    public async Task ManagementWritesRequireCsrfEvenForAdministrator()
    {
        await using var factory = new ApiFactory(clinicAdministrator: true);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });
        client.DefaultRequestHeaders.Add("Cookie", "__Host-PHCIS=" + factory.Token());
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/administrators",
            new { email = "second@example.test", confirmAdministratorAccess = true })).StatusCode);
    }

    [Fact]
    public void ClosedSlotCannotBeBookedOrUsedForRescheduling()
    {
        var slot = DomainTestData.Slot();
        slot.SetBlocked(true);
        Assert.Throws<DomainRuleException>(() => new Appointment(DomainTestData.Patient(), slot, new TestClock(DomainTestData.Now)));
        var appointment = DomainTestData.Appointment();
        var replacement = new TimeSlot(appointment.TimeSlot.Clinic, DomainTestData.Now.AddDays(1), DomainTestData.Now.AddDays(1).AddMinutes(30));
        replacement.SetBlocked(true);
        Assert.Throws<DomainRuleException>(() => appointment.Reschedule(replacement, new TestClock(DomainTestData.Now)));
    }
}
