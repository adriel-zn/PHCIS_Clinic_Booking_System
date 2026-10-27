using Microsoft.EntityFrameworkCore;
using PHCIS.Domain.Entities;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using PHCIS.Infrastructure.Identity;

namespace PHCIS.Infrastructure.Persistence;

/// <summary>
/// Coordinates persistence for the clinic model within a single dependency-injection scope.
/// Migrations are applied explicitly by deployment tooling, never by this context.
/// </summary>
public sealed class PhcisDbContext(DbContextOptions<PhcisDbContext> options) : IdentityDbContext<PortalUser>(options)
{
    public DbSet<Patient> Patients => Set<Patient>();
    public DbSet<Clinic> Clinics => Set<Clinic>();
    public DbSet<Appointment> Appointments => Set<Appointment>();
    public DbSet<TimeSlot> TimeSlots => Set<TimeSlot>();
    public DbSet<StaffClinic> StaffClinics => Set<StaffClinic>();
    public DbSet<ActivityLog> ActivityLogs => Set<ActivityLog>();
    public DbSet<ErrorLog> ErrorLogs => Set<ErrorLog>();
    public DbSet<AppointmentNotification> AppointmentNotifications => Set<AppointmentNotification>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.Entity<StaffClinic>().HasKey(x => new { x.UserId, x.ClinicId });
        modelBuilder.Entity<StaffClinic>().HasOne<PortalUser>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<StaffClinic>().HasOne<Clinic>().WithMany().HasForeignKey(x => x.ClinicId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<AppointmentNotification>().HasIndex(x => new { x.AppointmentId, x.Kind, x.Generation }).IsUnique();
        modelBuilder.Entity<AppointmentNotification>().Property(x => x.Kind).HasMaxLength(30);
        modelBuilder.Entity<AppointmentNotification>().Property(x => x.Status).HasMaxLength(30);
        modelBuilder.Entity<AppointmentNotification>().HasOne<Appointment>().WithMany().HasForeignKey(x => x.AppointmentId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<ActivityLog>().Property(x => x.Level).HasMaxLength(20);
        modelBuilder.Entity<ErrorLog>().Property(x => x.Level).HasMaxLength(20);
        modelBuilder.Entity<PortalUser>().HasIndex(user => user.PatientId).IsUnique();
        modelBuilder.Entity<PortalUser>().HasOne<Patient>().WithOne()
            .HasForeignKey<PortalUser>(user => user.PatientId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(PhcisDbContext).Assembly);
    }
}

