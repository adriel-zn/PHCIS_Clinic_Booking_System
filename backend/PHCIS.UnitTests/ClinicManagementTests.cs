using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PHCIS.Application.Services;
using PHCIS.Infrastructure.Persistence;

namespace PHCIS.UnitTests;

public sealed class ClinicManagementTests
{
    [Fact]
    public async Task PatientsCannotCreateClinics()
    {
        await using var factory = new ApiFactory();
        using var client = factory.Client(factory.Token());
        var access = await ApiTests.Json(await client.GetAsync("/api/clinic-management/access"));
        Assert.False(access.GetProperty("canManageClinics").GetBoolean());

        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync("/api/clinic-management",
            new CreateClinicRequest("New clinic", "10 Main Street", null))).StatusCode);
    }

    [Fact]
    public async Task AdministratorCreatesClinicAndAvailabilityAndRejectsDuplicates()
    {
        await using var factory = new ApiFactory(clinicAdministrator: true);
        using var client = factory.Client(factory.Token());
        var request = new CreateClinicRequest(" New clinic ", "10 Main Street", "031 555 0100");

        var response = await client.PostAsJsonAsync("/api/clinic-management", request);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var id = (await ApiTests.Json(response)).GetProperty("id").GetGuid();
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<PhcisDbContext>();
        Assert.Equal("New clinic", (await context.Clinics.SingleAsync()).Name);
        var slots = await context.TimeSlots.Where(x => x.ClinicId == id).ToListAsync();
        Assert.NotEmpty(slots);
        Assert.All(slots, slot =>
        {
            Assert.True(slot.StartsAt > DomainTestData.Now);
            Assert.Equal(TimeSpan.FromMinutes(30), slot.EndsAt - slot.StartsAt);
            var local = slot.StartsAt.ToOffset(TimeSpan.FromHours(2));
            Assert.InRange(local.Hour, 8, 16);
            Assert.DoesNotContain(local.DayOfWeek, new[] { DayOfWeek.Saturday, DayOfWeek.Sunday });
        });
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync("/api/clinic-management", request)).StatusCode);
        Assert.Equal(1, await context.Clinics.CountAsync());
        Assert.Equal(slots.Count, await context.TimeSlots.CountAsync());
    }

    [Fact]
    public async Task AdministratorCannotSaveInvalidDetails()
    {
        await using var factory = new ApiFactory(clinicAdministrator: true);
        using var client = factory.Client(factory.Token());
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/clinic-management",
            new CreateClinicRequest(" ", "", new string('1', 31)))).StatusCode);
        using var scope = factory.Services.CreateScope();
        Assert.Empty(await scope.ServiceProvider.GetRequiredService<PhcisDbContext>().Clinics.ToListAsync());
    }

    [Fact]
    public async Task AnonymousUsersCannotReadManagementAccess()
    {
        await using var factory = new ApiFactory();
        using var client = factory.Client();
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/clinic-management/access")).StatusCode);
    }
}
