using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using PHCIS.Api.Notifications;
using PHCIS.Api.Realtime;
using PHCIS.Domain.Entities;
using PHCIS.Infrastructure.Persistence;
using PHCIS.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;

namespace PHCIS.UnitTests;

public sealed class RealtimeAndReminderTests
{
    [Fact]
    public async Task BookingStagesEmailAtomicallyAndBroadcastsNoPatientPayload()
    {
        await using var factory = new ApiFactory();
        await factory.SeedAsync();
        using var client = factory.Client(factory.Token());
        Assert.Equal(HttpStatusCode.Created, (await client.PostAsJsonAsync("/api/appointments", new { timeSlotId = factory.SlotId })).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync("/api/appointments", new { timeSlotId = factory.SlotId })).StatusCode);
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<PhcisDbContext>();
        Assert.Equal(2, await context.AppointmentNotifications.CountAsync());
        var hub = new CapturedHub();
        var effects = new AppointmentEffects(context, hub, new TestClock(DomainTestData.Now), new ConfigurationBuilder().Build(), NullLogger<AppointmentEffects>.Instance);
        await effects.CommittedAsync(await context.Appointments.Include(a => a.TimeSlot).SingleAsync(), default);
        Assert.Equal(new[] { "test-subject" }, hub.Recipients);
        Assert.Equal("AppointmentsChanged", hub.Method);
        Assert.Empty(hub.Arguments);
        context.Users.Add(new PortalUser { Id = "assigned-staff", Email = "assigned@example.test", UserName = "assigned@example.test", PatientId = Guid.NewGuid() });
        context.UserRoles.Add(new IdentityUserRole<string> { UserId = "assigned-staff", RoleId = (await context.Roles.SingleAsync(r => r.Name == PortalRoles.Staff)).Id });
        var assignment = new StaffClinic { UserId = "assigned-staff", ClinicId = factory.ClinicId };
        context.StaffClinics.Add(assignment);
        await context.SaveChangesAsync();
        await effects.CommittedAsync(await context.Appointments.Include(a => a.TimeSlot).SingleAsync(), default);
        Assert.Contains("assigned-staff", hub.Recipients);
        context.StaffClinics.Remove(assignment);
        await context.SaveChangesAsync();
        await effects.CommittedAsync(await context.Appointments.Include(a => a.TimeSlot).SingleAsync(), default);
        Assert.DoesNotContain("assigned-staff", hub.Recipients);
        using var anonymous = factory.Client();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.PostAsync("/hubs/appointments/negotiate?negotiateVersion=1", null)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsync("/hubs/appointments/negotiate?negotiateVersion=1", null)).StatusCode);
    }

    [Fact]
    public async Task ReminderWaitsUntilDueAndIsProcessedOnlyOnce()
    {
        await using var factory = new ApiFactory();
        await factory.SeedAsync();
        using var client = factory.Client(factory.Token());
        await client.PostAsJsonAsync("/api/appointments", new { timeSlotId = factory.SlotId });
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<PhcisDbContext>();
        var mail = new CapturedEmail();
        var early = new AppointmentEmailDispatcher(context, mail, new TestClock(DomainTestData.Now), NullLogger<AppointmentEmailDispatcher>.Instance);
        await early.DispatchAsync(default);
        Assert.Equal(1, mail.Messages); // Confirmation; reminder is due five minutes later.
        var due = new AppointmentEmailDispatcher(context, mail, new TestClock(DomainTestData.Now.AddMinutes(6)), NullLogger<AppointmentEmailDispatcher>.Instance);
        await due.DispatchAsync(default);
        await due.DispatchAsync(default);
        Assert.Equal(2, mail.Messages);
    }

    private sealed class CapturedEmail : IEmailDelivery
    {
        public int Messages;
        public Task SendAsync(string recipient, string subject, string body, CancellationToken ct) { Messages++; return Task.CompletedTask; }
    }
    private sealed class CapturedHub : IHubContext<AppointmentHub>, IHubClients, IClientProxy
    {
        public IReadOnlyList<string> Recipients = [];
        public string? Method;
        public object?[] Arguments = [];
        IHubClients IHubContext<AppointmentHub>.Clients => this;
        IGroupManager IHubContext<AppointmentHub>.Groups => throw new NotSupportedException();
        public IClientProxy Users(IReadOnlyList<string> userIds) { Recipients = userIds; return this; }
        public Task SendCoreAsync(string method, object?[] args, CancellationToken cancellationToken = default) { Method = method; Arguments = args; return Task.CompletedTask; }
        public IClientProxy All => throw new NotSupportedException();
        public IClientProxy AllExcept(IReadOnlyList<string> excludedConnectionIds) => throw new NotSupportedException();
        public IClientProxy Client(string connectionId) => throw new NotSupportedException();
        public IClientProxy Clients(IReadOnlyList<string> connectionIds) => throw new NotSupportedException();
        public IClientProxy Group(string groupName) => throw new NotSupportedException();
        public IClientProxy GroupExcept(string groupName, IReadOnlyList<string> excludedConnectionIds) => throw new NotSupportedException();
        public IClientProxy Groups(IReadOnlyList<string> groupNames) => throw new NotSupportedException();
        public IClientProxy User(string userId) => throw new NotSupportedException();
    }
}
