using AutoMapper;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PHCIS.Application;
using PHCIS.Application.Models;
using PHCIS.Application.Validation;
using PHCIS.Domain.Entities;
using PHCIS.Infrastructure;
using PHCIS.Infrastructure.Persistence;

namespace PHCIS.UnitTests;

public sealed class InfrastructureTests
{
    private static PhcisDbContext CreateContext() => new(
        new DbContextOptionsBuilder<PhcisDbContext>()
            .UseSqlServer("Server=localhost;Database=PHCIS_ModelTests;Integrated Security=True;Encrypt=True")
            .Options);

    [Fact]
    public void SqlServerModelProtectsRelationshipsAndConcurrency()
    {
        using var context = CreateContext();
        var model = context.GetService<IDesignTimeModel>().Model;
        var domainTypes = model.GetEntityTypes().Where(x => x.ClrType.Namespace == "PHCIS.Domain.Entities").ToArray();
        Assert.Equal(4, domainTypes.Length);
        Assert.All(domainTypes, entity =>
        {
            var version = entity.FindProperty("RowVersion")!;
            Assert.True(version.IsConcurrencyToken);
            Assert.Equal(ValueGenerated.OnAddOrUpdate, version.ValueGenerated);
            Assert.All(entity.GetForeignKeys(), fk => Assert.Equal(DeleteBehavior.Restrict, fk.DeleteBehavior));
        });
        var appointment = model.FindEntityType(typeof(Appointment))!;
        var slotIndex = Assert.Single(appointment.GetIndexes(), index =>
            index.Properties.Select(p => p.Name).SequenceEqual([nameof(Appointment.TimeSlotId)]));
        Assert.True(slotIndex.IsUnique);
        Assert.Equal("[Status] <> 0", slotIndex.GetFilter());
        Assert.Contains(model.FindEntityType(typeof(TimeSlot))!.GetCheckConstraints(),
            constraint => constraint.Sql == "[EndsAt] > [StartsAt]");
    }

    [Fact]
    public void InitialMigrationGeneratesSqlServerConstraintsWithoutConnecting()
    {
        using var context = CreateContext();

        var script = context.GetService<Microsoft.EntityFrameworkCore.Migrations.IMigrator>().GenerateScript();

        Assert.Contains("CREATE TABLE [Patients]", script);
        Assert.Contains("CREATE TABLE [Clinics]", script);
        Assert.Contains("CREATE TABLE [TimeSlots]", script);
        Assert.Contains("CREATE TABLE [Appointments]", script);
        Assert.Contains("WHERE [Status] <> 0", script);
        Assert.Contains("rowversion", script);
        Assert.Contains("CK_TimeSlots_EndAfterStart", script);
        Assert.Contains("CK_Appointments_Cancellation", script);
        Assert.Contains("[CancellationReason] nvarchar(500)", script);
    }

    [Fact]
    public void RegistrationResolvesContextValidatorsAndMappings()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?>
            {
                ["ConnectionStrings:Phcis"] = "Server=localhost;Database=PHCIS_ModelTests;Integrated Security=True"
            }).Build();

        var services = new ServiceCollection();

        services.AddLogging();

        services.AddApplication(configuration).AddInfrastructure(configuration);

        using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        using var scope = provider.CreateScope();

        Assert.IsType<PhcisDbContext>(scope.ServiceProvider.GetRequiredService<PhcisDbContext>());

        Assert.IsType<PHCIS.Application.Services.AppointmentService>(
            scope.ServiceProvider.GetRequiredService<PHCIS.Application.Services.IAppointmentService>());

        Assert.IsType<PHCIS.Application.Services.AvailabilityService>(
            scope.ServiceProvider.GetRequiredService<PHCIS.Application.Services.IAvailabilityService>());

        Assert.IsType<PatientValidator>(scope.ServiceProvider.GetRequiredService<FluentValidation.IValidator<Patient>>());

        var mapper = scope.ServiceProvider.GetRequiredService<IMapper>();
        mapper.ConfigurationProvider.AssertConfigurationIsValid();
        var patient = DomainTestData.Patient();
        Assert.Equal(patient.Id, mapper.Map<PatientDto>(patient).Id);
        var appointment = DomainTestData.Appointment();
        appointment.Cancel("Cannot attend", new TestClock(DomainTestData.Now));
        var dto = mapper.Map<AppointmentDto>(appointment);
        Assert.Equal(AppointmentStatus.Cancelled, dto.Status);
        Assert.Equal(appointment.CancelledAt, dto.CancelledAt);
        Assert.Equal(appointment.CancellationReason, dto.CancellationReason);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-15)]
    public void TimeSlotValidationRejectsNonPositiveDuration(int minutes)
    {
        Assert.Throws<PHCIS.Domain.Exceptions.DomainRuleException>(() => new TimeSlot(
            DomainTestData.Clinic(), DomainTestData.Now, DomainTestData.Now.AddMinutes(minutes)));
    }

    [Fact]
    public void InfrastructureRequiresExplicitConnectionConfiguration()
    {
        var services = new ServiceCollection();
        Assert.Throws<InvalidOperationException>(() =>
            services.AddInfrastructure(new ConfigurationBuilder().Build()));
    }
}

