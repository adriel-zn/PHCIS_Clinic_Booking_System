using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PHCIS.Domain.Entities;
using PHCIS.Infrastructure.Persistence;

namespace PHCIS.UnitTests;

public sealed class StaffAppointmentTests
{
    [Theory]
    [InlineData("completion", AppointmentStatus.Completed)]
    [InlineData("no-show", AppointmentStatus.NoShow)]
    public async Task StaffFinalizesEndedScheduledAppointmentAndPatientSeesUpdatedStatus(string action, AppointmentStatus expected)
    {
        await using var factory = new ApiFactory(staffMember: true);
        await factory.SeedAsync();
        using var client = factory.Client(factory.Token());
        var id = await AddAppointmentAsync(factory, DomainTestData.Now.AddHours(-2), DomainTestData.Now);
        var path = $"/api/staff-appointments/{factory.ClinicId}/{id}/{action}";
        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsJsonAsync(path, new { confirmStatusChange = true })).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync(path, new { confirmStatusChange = true })).StatusCode);
        using var scope = factory.Services.CreateScope();
        Assert.Equal(expected, (await scope.ServiceProvider.GetRequiredService<PhcisDbContext>().Appointments.SingleAsync()).Status);
        var history = await ApiTests.Json(await client.GetAsync("/api/appointments"));
        Assert.Equal(expected.ToString(), history[0].GetProperty("status").GetString());
    }

    [Fact]
    public async Task StaffListScopesByClinicDayStatusAndPaginationAndExcludesUnneededPatientData()
    {
        await using var factory = new ApiFactory(staffMember: true);
        await factory.SeedAsync();
        using var client = factory.Client(factory.Token());
        // South African 26 October begins at 22:00 UTC on 25 October.
        var start = new DateTimeOffset(2026, 10, 25, 22, 15, 0, TimeSpan.Zero);
        await AddAppointmentAsync(factory, start, start.AddMinutes(30));
        await AddAppointmentAsync(factory, start.AddHours(1), start.AddHours(1.5));
        await AddAppointmentAsync(factory, start.AddDays(-1), start.AddDays(-1).AddMinutes(30));
        await AddAppointmentAsync(factory, start, start.AddMinutes(30), otherClinic: true);
        var query = $"/api/staff-appointments?clinicId={factory.ClinicId}&date=2026-10-26&limit=1";
        var first = await ApiTests.Json(await client.GetAsync(query));
        Assert.True(first.GetProperty("hasMore").GetBoolean());
        Assert.Equal(1, first.GetProperty("items").GetArrayLength());
        Assert.True(first.GetProperty("items")[0].GetProperty("canFinalize").GetBoolean());
        var second = await ApiTests.Json(await client.GetAsync(query + "&offset=1"));
        Assert.False(second.GetProperty("hasMore").GetBoolean());
        Assert.NotEqual(first.GetProperty("items")[0].GetProperty("id").GetString(), second.GetProperty("items")[0].GetProperty("id").GetString());
        var completed = await ApiTests.Json(await client.GetAsync(query + "&status=Completed"));
        Assert.Equal(0, completed.GetProperty("items").GetArrayLength());
        Assert.DoesNotContain("dateOfBirth", first.GetRawText());
        Assert.DoesNotContain("phoneNumber", first.GetRawText());
        Assert.DoesNotContain("nationalId", first.GetRawText());
    }

    [Theory]
    [InlineData("completion")]
    [InlineData("no-show")]
    public async Task FutureAndInProgressAppointmentsCannotBeFinalized(string action)
    {
        await using var factory = new ApiFactory(staffMember: true);
        await factory.SeedAsync();
        using var client = factory.Client(factory.Token());
        var id = await AddAppointmentAsync(factory, DomainTestData.Now.AddMinutes(-15), DomainTestData.Now.AddMinutes(15));
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync($"/api/staff-appointments/{factory.ClinicId}/{id}/{action}",
            new { confirmStatusChange = true })).StatusCode);
        var future = await AddAppointmentAsync(factory, DomainTestData.Now.AddHours(1), DomainTestData.Now.AddHours(2));
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync($"/api/staff-appointments/{factory.ClinicId}/{future}/{action}",
            new { confirmStatusChange = true })).StatusCode);
        using var scope = factory.Services.CreateScope();
        Assert.All(await scope.ServiceProvider.GetRequiredService<PhcisDbContext>().Appointments.ToListAsync(), x => Assert.Equal(AppointmentStatus.Scheduled, x.Status));
    }

    [Theory]
    [InlineData(AppointmentStatus.Cancelled)]
    [InlineData(AppointmentStatus.Completed)]
    [InlineData(AppointmentStatus.NoShow)]
    public async Task TerminalAppointmentsCannotBeChanged(AppointmentStatus status)
    {
        await using var factory = new ApiFactory(staffMember: true);
        await factory.SeedAsync();
        using var client = factory.Client(factory.Token());
        var id = await AddAppointmentAsync(factory, DomainTestData.Now.AddHours(-2), DomainTestData.Now.AddHours(-1), status: status);
        foreach (var action in new[] { "completion", "no-show" })
            Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync($"/api/staff-appointments/{factory.ClinicId}/{id}/{action}",
                new { confirmStatusChange = true })).StatusCode);
    }

    [Fact]
    public async Task UnknownAndMismatchedClinicAppointmentsReturnNotFound()
    {
        await using var factory = new ApiFactory(staffMember: true);
        await factory.SeedAsync();
        using var client = factory.Client(factory.Token());
        var id = await AddAppointmentAsync(factory, DomainTestData.Now.AddHours(-2), DomainTestData.Now.AddHours(-1));
        Assert.Equal(HttpStatusCode.NotFound, (await client.PostAsJsonAsync($"/api/staff-appointments/{Guid.NewGuid()}/{id}/completion",
            new { confirmStatusChange = true })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.PostAsJsonAsync($"/api/staff-appointments/{factory.ClinicId}/{Guid.NewGuid()}/no-show",
            new { confirmStatusChange = true })).StatusCode);
    }

    [Theory]
    [InlineData("date=2026-10-26&limit=101")]
    [InlineData("date=2026-10-26&offset=-1")]
    [InlineData("date=2026-10-26&status=Unknown")]
    [InlineData("")]
    public async Task StaffQueryValidationRejectsInvalidFilters(string query)
    {
        await using var factory = new ApiFactory(staffMember: true);
        using var client = factory.Client(factory.Token());
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync($"/api/staff-appointments?clinicId={factory.ClinicId}&{query}")).StatusCode);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PatientAndAdminWithoutStaffCannotReadOrFinalizeClinicAppointments(bool administrator)
    {
        await using var factory = new ApiFactory(clinicAdministrator: administrator);
        using var client = factory.Client(factory.Token());
        Assert.False((await ApiTests.Json(await client.GetAsync("/api/staff-appointments/access"))).GetProperty("canViewAppointments").GetBoolean());
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync($"/api/staff-appointments?clinicId={factory.ClinicId}&date=2026-10-26")).StatusCode);
        foreach (var action in new[] { "completion", "no-show" })
            Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync($"/api/staff-appointments/{factory.ClinicId}/{Guid.NewGuid()}/{action}",
                new { confirmStatusChange = true })).StatusCode);
    }

    [Fact]
    public async Task RemovingStaffRoleRevokesExistingSessionDashboardAccess()
    {
        await using var factory = new ApiFactory(clinicAdministrator: true, staffMember: true);
        using var client = factory.Client(factory.Token());
        Assert.True((await ApiTests.Json(await client.GetAsync("/api/staff-appointments/access"))).GetProperty("canViewAppointments").GetBoolean());
        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsJsonAsync("/api/staff/test-subject/removal", new { confirmRemoval = true })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync($"/api/staff-appointments?clinicId={factory.ClinicId}&date=2026-10-26")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync($"/api/staff-appointments/{factory.ClinicId}/{Guid.NewGuid()}/completion",
            new { confirmStatusChange = true })).StatusCode);
    }

    [Fact]
    public async Task FinalizationRequiresCsrfAndExplicitConfirmation()
    {
        await using var factory = new ApiFactory(staffMember: true);
        using var staff = factory.Client(factory.Token());
        var path = $"/api/staff-appointments/{factory.ClinicId}/{Guid.NewGuid()}/completion";
        Assert.Equal(HttpStatusCode.BadRequest, (await staff.PostAsJsonAsync(path, new { confirmStatusChange = false })).StatusCode);
        using var raw = factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });
        raw.DefaultRequestHeaders.Add("Cookie", "__Host-PHCIS=" + factory.Token());
        Assert.Equal(HttpStatusCode.BadRequest, (await raw.PostAsJsonAsync(path, new { confirmStatusChange = true })).StatusCode);
        using var anonymous = factory.Client();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("/api/staff-appointments")).StatusCode);
    }

    private static async Task<Guid> AddAppointmentAsync(ApiFactory factory, DateTimeOffset start, DateTimeOffset end,
        bool otherClinic = false, AppointmentStatus status = AppointmentStatus.Scheduled)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<PhcisDbContext>();
        var clinic = otherClinic ? DomainTestData.Clinic() : await context.Clinics.SingleAsync(x => x.Id == factory.ClinicId);
        var slot = new TimeSlot(clinic, start, end);
        var appointment = new Appointment(await context.Patients.SingleAsync(x => x.Id == factory.PatientId), slot,
            new TestClock(start.AddHours(-1)));
        if (status == AppointmentStatus.Cancelled)
            appointment.Cancel("Test cancellation", new TestClock(start.AddMinutes(-30)));
        if (status == AppointmentStatus.Completed)
            appointment.Complete(new TestClock(end));
        if (status == AppointmentStatus.NoShow)
            appointment.MarkNoShow(new TestClock(end));
        context.Appointments.Add(appointment);
        await context.SaveChangesAsync();
        return appointment.Id;
    }
}
