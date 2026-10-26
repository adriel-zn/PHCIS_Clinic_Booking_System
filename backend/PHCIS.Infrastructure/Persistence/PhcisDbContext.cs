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

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.Entity<PortalUser>().HasIndex(user => user.PatientId).IsUnique();
        modelBuilder.Entity<PortalUser>().HasOne<Patient>().WithOne()
            .HasForeignKey<PortalUser>(user => user.PatientId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(PhcisDbContext).Assembly);
    }
}

