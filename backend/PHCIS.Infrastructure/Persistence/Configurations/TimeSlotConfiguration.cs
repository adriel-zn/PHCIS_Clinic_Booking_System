using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PHCIS.Domain.Entities;

namespace PHCIS.Infrastructure.Persistence.Configurations;

/// <summary>
/// Defines the SQL Server persistence mapping for TimeSlot.
/// </summary>
public sealed class TimeSlotConfiguration : IEntityTypeConfiguration<TimeSlot>
{
    public void Configure(EntityTypeBuilder<TimeSlot> builder)
    {
        builder.ToTable("TimeSlots", table =>
            table.HasCheckConstraint("CK_TimeSlots_EndAfterStart", "[EndsAt] > [StartsAt]"));
        builder.HasKey(x => x.Id);
        builder.Property(x => x.RowVersion)
            .HasField("_rowVersion")
            .UsePropertyAccessMode(PropertyAccessMode.Field)
            .IsRowVersion();
        builder.HasOne(x => x.Clinic)
            .WithMany(x => x.TimeSlots)
            .HasForeignKey(x => x.ClinicId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => new { x.ClinicId, x.StartsAt, x.EndsAt })
            .IsUnique();
    }
}
