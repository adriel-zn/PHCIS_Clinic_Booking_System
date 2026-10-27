using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PHCIS.Infrastructure.Persistence;
using PHCIS.Infrastructure.Persistence.Repositories;
using PHCIS.Application.Abstractions;

namespace PHCIS.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Phcis");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException("Configure ConnectionStrings:Phcis in API appsettings.json, user secrets or environment variables.");
        }
        services.AddDbContext<PhcisDbContext>(options =>
            options.UseSqlServer(connectionString, sql => sql.EnableRetryOnFailure()));
        services.AddScoped<IPatientReader, PatientReader>();
        services.AddScoped<ITimeSlotRepository, TimeSlotRepository>();
        services.AddScoped<IAppointmentRepository, AppointmentRepository>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<IPortalReader, PortalReader>();
        services.AddScoped<IClinicWriter, ClinicWriter>();
        services.AddScoped<IStaffAppointmentReader, StaffAppointmentReader>();
        return services;
    }
}

