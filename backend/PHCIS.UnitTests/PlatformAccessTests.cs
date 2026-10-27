using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using PHCIS.Domain.Entities;
using PHCIS.Infrastructure.Identity;
using PHCIS.Infrastructure.Persistence;

namespace PHCIS.UnitTests;

public sealed class PlatformAccessTests
{
    [Fact]
    public async Task StaffCanOnlyReadAndChangeAppointmentsAtAssignedClinicsAndRemovalIsImmediate()
    {
        await using var factory = new ApiFactory(clinicAdministrator: true, staffMember: true);
        await factory.SeedAsync();
        using var client = factory.Client(factory.Token());
        Guid otherClinic;
        using (var scope = factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<PhcisDbContext>();
            var clinic = new Clinic("Other clinic", "Other address");
            context.Clinics.Add(clinic);
            await context.SaveChangesAsync();
            otherClinic = clinic.Id;
        }
        var query = $"/api/staff-appointments?clinicId={otherClinic}&date=2026-10-26";
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync(query)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync($"/api/staff-appointments/{otherClinic}/{Guid.NewGuid()}/completion", new { confirmStatusChange = true })).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsJsonAsync("/api/staff/test-subject/clinics", new { clinicId = otherClinic, confirmAssignment = true })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync(query)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsJsonAsync($"/api/staff/test-subject/clinics/{otherClinic}/removal", new { confirmRemoval = true })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync(query)).StatusCode);
    }

    [Fact]
    public async Task StaffEditsOnlyPatientsLinkedToTheirClinicsAndCannotChangeAccountState()
    {
        await using var factory = new ApiFactory(staffMember: true);
        await factory.SeedAsync();
        using var staff = factory.Client(factory.Token());
        using var patient = factory.Client();
        await RegisterAsync(patient, "linked@example.test");
        string id;
        using (var scope = factory.Services.CreateScope())
            id = (await scope.ServiceProvider.GetRequiredService<UserManager<PortalUser>>().FindByEmailAsync("linked@example.test"))!.Id;
        var profile = new { firstName = "Edited", lastName = "Patient", phoneNumber = "082 555 0100", dateOfBirth = "1990-01-01" };
        Assert.Equal(HttpStatusCode.NotFound, (await staff.PostAsJsonAsync($"/api/users/{id}/profile", profile)).StatusCode);
        await patient.PostAsJsonAsync("/api/account/login", new { email = "linked@example.test", password = "StrongDemo!2026", rememberMe = false });
        Assert.Equal(HttpStatusCode.Created, (await patient.PostAsJsonAsync("/api/appointments", new { timeSlotId = factory.SlotId })).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await staff.PostAsJsonAsync($"/api/users/{id}/profile", profile)).StatusCode);
        var list = await ApiTests.Json(await staff.GetAsync("/api/users"));
        Assert.Equal(1, list.GetProperty("items").GetArrayLength());
        Assert.Equal(HttpStatusCode.Forbidden, (await staff.PostAsJsonAsync($"/api/users/{id}/state", new { isActive = false, confirmChange = true })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await staff.PostAsJsonAsync("/api/users", new { })).StatusCode);
    }

    [Fact]
    public async Task DeactivationAndArchiveBlockLoginAndExistingSessionsAndProtectLastActiveAdmin()
    {
        await using var factory = new ApiFactory(clinicAdministrator: true);
        await factory.SeedAsync();
        using var admin = factory.Client(factory.Token());
        using var target = factory.Client();
        await RegisterAsync(target, "target@example.test");
        await target.PostAsJsonAsync("/api/account/login", new { email = "target@example.test", password = "StrongDemo!2026", rememberMe = false });
        string id;
        using (var scope = factory.Services.CreateScope())
            id = (await scope.ServiceProvider.GetRequiredService<UserManager<PortalUser>>().FindByEmailAsync("target@example.test"))!.Id;
        Assert.Equal(HttpStatusCode.Conflict, (await admin.PostAsJsonAsync("/api/users/test-subject/state", new { isActive = false, confirmChange = true })).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await admin.PostAsJsonAsync($"/api/users/{id}/state", new { isActive = false, confirmChange = true })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await target.GetAsync("/api/account/me")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await target.PostAsJsonAsync("/api/account/login", new { email = "target@example.test", password = "StrongDemo!2026", rememberMe = false })).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await admin.PostAsJsonAsync($"/api/users/{id}/state", new { isActive = true, confirmChange = true })).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await admin.PostAsJsonAsync($"/api/users/{id}/state", new { delete = true, confirmChange = true })).StatusCode);
        using var fresh = factory.Services.CreateScope();
        var user = (await fresh.ServiceProvider.GetRequiredService<UserManager<PortalUser>>().FindByIdAsync(id))!;
        Assert.True(user.IsDeleted);
        Assert.False(user.IsActive);
        Assert.True(await fresh.ServiceProvider.GetRequiredService<PhcisDbContext>().Patients.AnyAsync(p => p.Id == user.PatientId));
    }

    [Fact]
    public async Task GraphQlUsesPatientOwnershipAndStaffClinicAssignments()
    {
        await using var factory = new ApiFactory(staffMember: true);
        await factory.SeedAsync();
        using var client = factory.Client(factory.Token());
        Assert.Equal(HttpStatusCode.Created, (await client.PostAsJsonAsync("/api/appointments", new { timeSlotId = factory.SlotId })).StatusCode);
        var own = await ApiTests.Json(await client.PostAsJsonAsync("/graphql", new { query = "{ myAppointments(offset:0, limit:10) { id clinicId status } }" }));
        Assert.False(own.TryGetProperty("errors", out _), own.GetRawText());
        Assert.Equal(1, own.GetProperty("data").GetProperty("myAppointments").GetArrayLength());
        var scopedQuery = $"{{ clinicAppointments(clinicId:\"{factory.ClinicId}\",date:\"2026-10-26\",status:null,offset:0,limit:10) {{ items {{ id patientName }} }} }}";
        var allowed = await ApiTests.Json(await client.PostAsJsonAsync("/graphql", new { query = scopedQuery }));
        Assert.False(allowed.TryGetProperty("errors", out _), allowed.GetRawText());
        using (var scope = factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<PhcisDbContext>();
            context.StaffClinics.RemoveRange(context.StaffClinics);
            await context.SaveChangesAsync();
        }
        var denied = await ApiTests.Json(await client.PostAsJsonAsync("/graphql", new { query = scopedQuery }));
        Assert.True(denied.TryGetProperty("errors", out _));
        using var anonymous = factory.Client();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.PostAsJsonAsync("/graphql", new { query = "{ myAppointments(offset:0,limit:10) { id } }" })).StatusCode);
    }

    private static async Task RegisterAsync(HttpClient client, string email) =>
        Assert.Equal(HttpStatusCode.Created, (await client.PostAsJsonAsync("/api/account/register", new
        {
            firstName = "Test",
            lastName = "Patient",
            email,
            password = "StrongDemo!2026",
            phoneNumber = "082 555 0100",
            dateOfBirth = "1990-01-01"
        })).StatusCode);
}
