using FluentValidation;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using PHCIS.Application.Services;

namespace PHCIS.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services, IConfiguration configuration)
    {
        services.TryAddSingleton(TimeProvider.System);

        services.AddAutoMapper(config =>
        {
            config.LicenseKey = configuration["AutoMapper:LicenseKey"];
        }, typeof(DependencyInjection).Assembly);
        services.AddValidatorsFromAssembly(typeof(DependencyInjection).Assembly);
        services.AddScoped<IAppointmentService, AppointmentService>();
        services.AddScoped<IAvailabilityService, AvailabilityService>();
        return services;
    }
}

