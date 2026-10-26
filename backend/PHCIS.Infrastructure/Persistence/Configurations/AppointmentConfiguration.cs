using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PHCIS.Domain.Entities;

namespace PHCIS.Infrastructure.Persistence.Configurations;

public sealed class AppointmentConfiguration : IEntityTypeConfiguration<Appointment>
{
    public void Configure(EntityTypeBuilder<Appointment> builder)
    {
        builder.ToTable("Appointments", table =>
        {
            table.HasCheckConstraint("CK_Appointments_Status", "[Status] IN (0, 1, 2, 3)");
            table.HasCheckConstraint("CK_Appointments_Cancellation",
                "([Status] = 0 AND [CancelledAt] IS NOT NULL AND [CancelledAt] >= [CreatedAt] " +
                "AND [CancellationReason] IS NOT NULL AND LEN(LTRIM(RTRIM([CancellationReason]))) > 0) " +
                "OR ([Status] <> 0 AND [CancelledAt] IS NULL AND [CancellationReason] IS NULL)");
        });

        builder.Property(x => x.CancellationReason).HasMaxLength(500);
        builder.Property(x => x.ReasonForVisit).HasMaxLength(200).IsRequired();

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Status).HasConversion<int>();

        builder.Property(x => x.RowVersion)
            .HasField("_rowVersion")
            .UsePropertyAccessMode(PropertyAccessMode.Field)
            .IsRowVersion();

        builder.HasOne(x => x.Patient)
            .WithMany(x => x.Appointments)
            .HasForeignKey(x => x.PatientId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.TimeSlot)
            .WithMany(x => x.Appointments)
            .HasForeignKey(x => x.TimeSlotId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => x.TimeSlotId)
            .IsUnique()
            .HasFilter("[Status] <> 0");

        builder.HasIndex(x => new { x.PatientId, x.CreatedAt });
    }
}
