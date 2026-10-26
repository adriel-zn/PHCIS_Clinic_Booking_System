using System.Net;
using System.Net.Mail;
using Microsoft.EntityFrameworkCore;
using PHCIS.Domain.Entities;
using PHCIS.Infrastructure.Persistence;

namespace PHCIS.Api.Notifications;

/// <summary>Transport boundary for development or externally configured SMTP delivery.</summary>
public interface IEmailDelivery
{
    /// <summary>Sends a plain-text message through the configured transport.</summary>
    Task SendAsync(string recipient, string subject, string body, CancellationToken cancellationToken);
}
/// <summary>SMTP adapter suitable for a local Mailpit inbox or a configured authenticated SMTP provider.</summary>
public sealed class SmtpEmailDelivery(IConfiguration configuration) : IEmailDelivery
{
    /// <summary>Sends one plain-text message using the configured SMTP transport.</summary>
    public async Task SendAsync(string recipient, string subject, string body, CancellationToken cancellationToken)
    {
        using var client = new SmtpClient(configuration["Email:SmtpHost"] ?? "localhost", configuration.GetValue<int?>("Email:SmtpPort") ?? 1025)
        {
            EnableSsl = configuration.GetValue<bool>("Email:EnableSsl"),
            Timeout = 10000
        };
        if (!string.IsNullOrWhiteSpace(configuration["Email:Username"]))
            client.Credentials = new NetworkCredential(configuration["Email:Username"], configuration["Email:Password"]);
        using var message = new MailMessage(configuration["Email:From"] ?? "no-reply@phcis.local", recipient, subject, body);
        await client.SendMailAsync(message, cancellationToken);
    }
}

/// <summary>Claims durable work with expiring leases; retries are bounded and do not retry appointment writes.</summary>
public sealed class AppointmentEmailDispatcher(PhcisDbContext context, IEmailDelivery email, TimeProvider clock,
    ILogger<AppointmentEmailDispatcher> logger)
{
    /// <summary>Claims due notification work and records delivery or retry state.</summary>
    public async Task DispatchAsync(CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        var ids = await context.AppointmentNotifications.AsNoTracking().Where(n => n.ProcessedAt == null && n.Attempts < 5 && n.DueAt <= now &&
            (n.LeaseUntil == null || n.LeaseUntil <= now)).OrderBy(n => n.DueAt).Take(20).Select(n => n.Id).ToListAsync(ct);
        foreach (var id in ids)
        {
            var available = context.AppointmentNotifications.Where(n => n.Id == id && n.ProcessedAt == null &&
                (n.LeaseUntil == null || n.LeaseUntil <= now));
            if (context.Database.IsRelational())
            {
                if (await available.ExecuteUpdateAsync(s => s.SetProperty(n => n.LeaseUntil, now.AddMinutes(2)).SetProperty(n => n.Attempts, n => n.Attempts + 1), ct) == 0)
                    continue;
            }
            var item = await context.AppointmentNotifications.SingleAsync(n => n.Id == id, ct);
            if (!context.Database.IsRelational())
            { item.Attempts++; item.LeaseUntil = now.AddMinutes(2); await context.SaveChangesAsync(ct); }
            try
            {
                var appointment = await context.Appointments.Include(a => a.Patient).Include(a => a.TimeSlot).ThenInclude(s => s.Clinic).SingleAsync(a => a.Id == item.AppointmentId, ct);
                var account = await context.Users.SingleOrDefaultAsync(u => u.PatientId == appointment.PatientId, ct);
                if (appointment.Status != AppointmentStatus.Scheduled || appointment.TimeSlot.StartsAt != item.SlotStartsAt || appointment.TimeSlot.SchedulingVersion != item.Generation || appointment.TimeSlot.StartsAt <= now ||
                    account is not { IsActive: true, IsDeleted: false } || string.IsNullOrWhiteSpace(account.Email))
                    item.Status = "Skipped";
                else
                {
                    var local = item.SlotStartsAt.ToOffset(TimeSpan.FromHours(2));
                    await email.SendAsync(account.Email, item.Kind == "Reminder" ? "PHCIS appointment reminder" : "PHCIS booking confirmation",
                        $"Your appointment at {appointment.TimeSlot.Clinic.Name} is scheduled for {local:dd MMM yyyy HH:mm} South African time. Reference: {appointment.Id}. Sign in to the portal to view or cancel it.", ct);
                    item.Status = "Sent";
                }
                item.ProcessedAt = now;
                logger.LogInformation("Appointment email work processed for {AppointmentId}.", item.AppointmentId);
            }
            catch (Exception) when (!ct.IsCancellationRequested)
            {
                item.Status = item.Attempts >= 5 ? "Failed" : "Pending";
                item.DueAt = now.AddMinutes(Math.Min(60, item.Attempts * 5));
                logger.LogError("Appointment email delivery failed for {AppointmentId}.", item.AppointmentId);
            }
            item.LeaseUntil = null;
            await context.SaveChangesAsync(ct);
        }
    }
}
/// <summary>Polls due notification work without blocking appointment requests.</summary>
public sealed class AppointmentEmailWorker(IServiceScopeFactory scopes, IConfiguration configuration, ILogger<AppointmentEmailWorker> logger) : BackgroundService
{
    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!configuration.GetValue<bool>("Email:Enabled"))
            return;
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                await scope.ServiceProvider.GetRequiredService<AppointmentEmailDispatcher>().DispatchAsync(stoppingToken);
            }
            catch (Exception) when (!stoppingToken.IsCancellationRequested) { logger.LogError("Appointment email polling failed."); }
            await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
        }
    }
}
