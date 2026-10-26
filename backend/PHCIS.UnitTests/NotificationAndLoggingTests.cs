using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using PHCIS.Api.Notifications;
using PHCIS.Api.Observability;
using PHCIS.Domain.Entities;
using PHCIS.Infrastructure.Persistence;
using Serilog.Events;
using Serilog.Parsing;

namespace PHCIS.UnitTests;

public sealed class NotificationAndLoggingTests
{
    [Fact]
    public async Task ConfirmationAndReminderAreDurableAndStaleOrCancelledWorkIsSkipped()
    {
        await using var factory = new ApiFactory();
        await factory.SeedAsync();
        using var client = factory.Client(factory.Token());
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<PhcisDbContext>();
        var patient = await context.Patients.SingleAsync();
        patient.UpdateContactDetails("082 555 0100", "patient@example.test");
        await context.SaveChangesAsync();
        var appointment = new Appointment(patient, await context.TimeSlots.Include(x => x.Clinic).SingleAsync(), new TestClock(DomainTestData.Now));
        context.Appointments.Add(appointment);
        context.AppointmentNotifications.Add(new AppointmentNotification { AppointmentId = appointment.Id, Generation = appointment.TimeSlot.SchedulingVersion, SlotStartsAt = appointment.TimeSlot.StartsAt, DueAt = DomainTestData.Now });
        await context.SaveChangesAsync();
        var email = new CapturedEmail();
        var dispatcher = new AppointmentEmailDispatcher(context, email, new TestClock(DomainTestData.Now), NullLogger<AppointmentEmailDispatcher>.Instance);
        await dispatcher.DispatchAsync(default);
        await dispatcher.DispatchAsync(default);
        Assert.Equal(1, email.Messages);
        context.AppointmentNotifications.Add(new AppointmentNotification { AppointmentId = appointment.Id, Generation = appointment.TimeSlot.SchedulingVersion, Kind = "Reminder", SlotStartsAt = appointment.TimeSlot.StartsAt, DueAt = DomainTestData.Now });
        appointment.Cancel("Changed plans", new TestClock(DomainTestData.Now));
        await context.SaveChangesAsync();
        await dispatcher.DispatchAsync(default);
        Assert.Equal(1, email.Messages);
        Assert.Contains(await context.AppointmentNotifications.ToListAsync(), n => n.Status == "Skipped");
    }

    [Fact]
    public async Task SerilogRoutesErrorsAndActivityToDifferentTablesAndRedactsValues()
    {
        await using var factory = new ApiFactory();
        var queue = new DatabaseLogQueue();
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["DatabaseLogging:Enabled"] = "true" }).Build();
        using var writer = new DatabaseLogWriter(queue, factory.Services.GetRequiredService<IServiceScopeFactory>(), configuration);
        await writer.StartAsync(default);
        var template = new MessageTemplateParser().Parse("Operation {ActorId} {Password}");
        queue.Emit(new LogEvent(DateTimeOffset.UtcNow, LogEventLevel.Information, null, template,
            [new LogEventProperty("ActorId", new ScalarValue("account-id")), new LogEventProperty("Password", new ScalarValue("secret-value"))]));
        queue.Emit(new LogEvent(DateTimeOffset.UtcNow, LogEventLevel.Error, new InvalidOperationException("private payload"), template, []));
        await writer.StopAsync(default);
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<PhcisDbContext>();
        var activity = await context.ActivityLogs.SingleAsync();
        var error = await context.ErrorLogs.SingleAsync();
        Assert.DoesNotContain("secret-value", activity.Properties);
        Assert.DoesNotContain("private payload", error.Event);
        Assert.Equal(typeof(InvalidOperationException).FullName, error.ExceptionType);
    }
    private sealed class CapturedEmail : IEmailDelivery
    {
        public int Messages;
        public Task SendAsync(string recipient, string subject, string body, CancellationToken ct) { Messages++; return Task.CompletedTask; }
    }
}
