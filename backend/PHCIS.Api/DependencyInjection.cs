using System.Reflection;
using Microsoft.EntityFrameworkCore;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using PHCIS.Infrastructure.Identity;
using PHCIS.Infrastructure.Persistence;
using Microsoft.OpenApi;
using PHCIS.Api.Authentication;
using PHCIS.Api.Errors;

namespace PHCIS.Api;

/// <summary>Registers transport, error handling, documentation, and token-based patient access.</summary>
public static class DependencyInjection
{
    /// <summary>Registers API concerns while leaving use cases and persistence in their own layers.</summary>
    public static IServiceCollection AddApi(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddCors(options => options.AddPolicy("Frontend", policy =>
        {
            var origins = configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
            if (origins.Length > 0)
            {
                policy.WithOrigins(origins).WithMethods("GET", "POST").AllowAnyHeader().AllowCredentials();
            }
        }));
        services.AddProblemDetails(options => options.CustomizeProblemDetails = context =>
        {
            context.ProblemDetails.Instance = context.HttpContext.Request.Path;
            context.ProblemDetails.Extensions["traceId"] = context.HttpContext.TraceIdentifier;
        });
        services.AddExceptionHandler<ApiExceptionHandler>();
        services.AddAntiforgery(options => { options.HeaderName = "X-CSRF-TOKEN"; options.Cookie.Name = configuration["Authentication:CsrfCookieName"] ?? "__Host-PHCIS-CSRF"; options.Cookie.SecurePolicy = CookieSecurePolicy.Always; options.Cookie.SameSite = SameSiteMode.Strict; });
        services.AddScoped<AccountService>();
        services.AddScoped<AdministratorService>();
        services.AddScoped<StaffService>();
        services.AddScoped<AccountAccess>();
        services.AddSignalR();
        services.AddGraphQLServer().AddQueryType<PHCIS.Api.GraphQL.PortalQuery>()
            .AddMaxExecutionDepthRule(8).ModifyRequestOptions(options => options.IncludeExceptionDetails = false);
        services.AddScoped<PHCIS.Application.Abstractions.IAppointmentEffects, PHCIS.Api.Realtime.AppointmentEffects>();
        services.AddScoped<PHCIS.Api.Notifications.IEmailDelivery, PHCIS.Api.Notifications.SmtpEmailDelivery>();
        services.AddScoped<PHCIS.Api.Notifications.AppointmentEmailDispatcher>();
        services.AddHostedService<PHCIS.Api.Notifications.AppointmentEmailWorker>();
        services.AddScoped<PHCIS.Api.Observability.EntityAuditInterceptor>();
        services.AddDbContext<PhcisDbContext>((sp, options) => options.AddInterceptors(sp.GetRequiredService<PHCIS.Api.Observability.EntityAuditInterceptor>()));
        services.AddHttpContextAccessor();
        services.AddScoped<ValidateCsrfFilter>();
        services.AddControllers(options => options.Filters.AddService<ValidateCsrfFilter>())
            .AddJsonOptions(options =>
            {
                // Reject unknown body fields, including attempts to supply a different patient ID.
                options.JsonSerializerOptions.UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow;
                options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
            })
            .ConfigureApiBehaviorOptions(options =>
            {
                options.InvalidModelStateResponseFactory = action =>
                {
                    var errors = action.ModelState
                        .Where(entry => entry.Value?.Errors.Count > 0)
                        .ToDictionary(entry => entry.Key, entry => entry.Value!.Errors
                            .Select(_ => "A required value is missing or has an invalid format.").Distinct().ToArray());
                    var details = new ValidationProblemDetails(errors)
                    {
                        Status = StatusCodes.Status400BadRequest,
                        Title = "Request validation failed.",
                        Instance = action.HttpContext.Request.Path
                    };
                    details.Extensions["traceId"] = action.HttpContext.TraceIdentifier;
                    var result = new BadRequestObjectResult(details);
                    result.ContentTypes.Add("application/problem+json");
                    return result;
                };
            });

        services.AddAuthentication(IdentityConstants.ApplicationScheme).AddIdentityCookies();
        services.AddIdentityCore<PortalUser>(options =>
        {
            options.User.RequireUniqueEmail = true;
            options.Password.RequiredLength = 10;
            options.Lockout.MaxFailedAccessAttempts = 5;
            options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
        })
        .AddRoles<IdentityRole>()
        .AddEntityFrameworkStores<PhcisDbContext>()
        .AddSignInManager()
        .AddDefaultTokenProviders()
        .AddClaimsPrincipalFactory<PatientClaimsFactory>();
        services.ConfigureApplicationCookie(options =>
        {
            options.Cookie.Name = configuration["Authentication:CookieName"] ?? "__Host-PHCIS";
            options.Cookie.HttpOnly = true;
            options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
            options.Cookie.SameSite = SameSiteMode.Strict;
            options.ExpireTimeSpan = TimeSpan.FromHours(8);
            options.SlidingExpiration = true;
            options.Events.OnRedirectToLogin = context => { context.Response.StatusCode = 401; return Task.CompletedTask; };
            options.Events.OnRedirectToAccessDenied = context => { context.Response.StatusCode = 403; return Task.CompletedTask; };
        });
        services.AddAuthorization(options =>
        {
            options.AddPolicy(PatientIdentity.Policy, policy =>
            {
                policy.RequireAuthenticatedUser();
                policy.RequireAssertion(context => PatientIdentity.TryGetPatientId(context.User, out _));
                policy.AddRequirements(new ActiveAccountRequirement());
            });
            options.AddPolicy(ClinicAdministration.Policy, policy =>
            {
                policy.RequireAuthenticatedUser();
                policy.AddRequirements(new ClinicAdministratorRequirement());
            });
            options.AddPolicy(StaffAccessRequirement.Policy, policy =>
            {
                policy.RequireAuthenticatedUser();
                policy.AddRequirements(new StaffAccessRequirement());
            });
        });
        services.AddScoped<Microsoft.AspNetCore.Authorization.IAuthorizationHandler, ClinicAdministratorHandler>();
        services.AddScoped<Microsoft.AspNetCore.Authorization.IAuthorizationHandler, StaffAccessHandler>();
        services.AddScoped<Microsoft.AspNetCore.Authorization.IAuthorizationHandler, ActiveAccountHandler>();

        services.AddSwaggerGen(options =>
        {
            options.SwaggerDoc("v1", new OpenApiInfo
            {
                Title = "PHCIS Appointment API",
                Version = "v1",
                Description = "Patient-scoped booking, cancellation, and future slot availability."
            });
            options.IncludeXmlComments(System.IO.Path.Combine(AppContext.BaseDirectory,
                $"{Assembly.GetExecutingAssembly().GetName().Name}.xml"));
            options.AddSecurityDefinition("session", new OpenApiSecurityScheme
            {
                Type = SecuritySchemeType.ApiKey,
                In = ParameterLocation.Cookie,
                Name = configuration["Authentication:CookieName"] ?? "__Host-PHCIS",
                Description = "EF Identity HttpOnly session cookie, set by /api/account/login."
            });
            options.AddSecurityDefinition("csrf", new OpenApiSecurityScheme
            {
                Type = SecuritySchemeType.ApiKey,
                In = ParameterLocation.Header,
                Name = "X-CSRF-TOKEN",
                Description = "Get requestToken from /api/account/csrf before an unsafe request."
            });
            options.OperationFilter<CookieSecurityOperationFilter>();
        });

        return services;
    }
}

