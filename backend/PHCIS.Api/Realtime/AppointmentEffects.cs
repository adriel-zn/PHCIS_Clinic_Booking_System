using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using PHCIS.Application.Abstractions;
using PHCIS.Domain.Entities;
using PHCIS.Infrastructure.Persistence;

namespace PHCIS.Api.Realtime;

/// <summary>Stages durable email and sends scoped, payload-free refresh signals after commit.</summary>
public sealed class AppointmentEffects(PhcisDbContext context, IHubContext<AppointmentHub> hub, TimeProvider clock,
    IConfiguration configuration, ILogger<AppointmentEffects> logger) : IAppointmentEffects
{
    /// <summary>Stages confirmation and reminder work before the appointment commits.</summary>
    public void Stage(Appointment appointment)
    {
        var now = clock.GetUtcNow();
        var start = appointment.TimeSlot.StartsAt;
        var lead = TimeSpan.FromHours(Math.Clamp(configuration.GetValue<int?>("Email:ReminderLeadHours") ?? 24, 1, 168));
        context.AppointmentNotifications.Add(new AppointmentNotification { AppointmentId = appointment.Id, Generation = appointment.TimeSlot.SchedulingVersion, SlotStartsAt = start, DueAt = now });
        var reminderAt = start - lead > now ? start - lead : now.AddMinutes(5);
        if (reminderAt < start)
            context.AppointmentNotifications.Add(new AppointmentNotification { AppointmentId = appointment.Id, Generation = appointment.TimeSlot.SchedulingVersion, SlotStartsAt = start, Kind = "Reminder", DueAt = reminderAt });
    }
    /// <summary>Notifies only the patient and currently assigned, active Staff accounts after commit.</summary>
    public async Task CommittedAsync(Appointment appointment, CancellationToken cancellationToken)
    {
        try
        {
            var staffRoleId = await context.Roles.Where(r => r.NormalizedName == "STAFF").Select(r => r.Id).FirstOrDefaultAsync(cancellationToken);
            var ids = await context.Users.Where(u => u.IsActive && !u.IsDeleted && (u.PatientId == appointment.PatientId ||
                context.StaffClinics.Any(sc => sc.UserId == u.Id && sc.ClinicId == appointment.TimeSlot.ClinicId) &&
                context.UserRoles.Any(ur => ur.UserId == u.Id && ur.RoleId == staffRoleId))).Select(u => u.Id).ToListAsync(cancellationToken);
            await hub.Clients.Users(ids).SendCoreAsync("AppointmentsChanged", [], cancellationToken);
        }
        catch (Exception) { logger.LogWarning("Real-time refresh delivery failed for {AppointmentId}.", appointment.Id); }
    }
}
