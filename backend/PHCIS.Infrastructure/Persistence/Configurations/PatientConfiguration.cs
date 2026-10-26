using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PHCIS.Domain.Entities;

namespace PHCIS.Infrastructure.Persistence.Configurations;

/// <summary>
/// Defines the SQL Server persistence mapping for Patient.
/// </summary>
public sealed class PatientConfiguration : IEntityTypeConfiguration<Patient>
{
    public void Configure(EntityTypeBuilder<Patient> builder)
    {
        builder.ToTable("Patients");
        builder.Property(x => x.NationalId).HasMaxLength(20);

        builder.HasKey(x => x.Id);

        builder.Property(x => x.FirstName)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(x => x.LastName)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(x => x.DateOfBirth).HasColumnType("date");
        builder.Property(x => x.PhoneNumber)
            .HasMaxLength(30);

        builder.Property(x => x.Email)
            .HasMaxLength(254);

        builder.Property(x => x.RowVersion)
            .HasField("_rowVersion")
            .UsePropertyAccessMode(PropertyAccessMode.Field)
            .IsRowVersion();

        builder.HasIndex(x => new { x.LastName, x.FirstName });
    }
}
