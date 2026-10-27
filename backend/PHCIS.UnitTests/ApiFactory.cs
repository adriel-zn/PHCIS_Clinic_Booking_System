using System.Net.Http.Json;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using PHCIS.Api.Authentication;
using PHCIS.Domain.Entities;
using PHCIS.Infrastructure.Persistence;
using PHCIS.Infrastructure.Identity;

namespace PHCIS.UnitTests;

/// <summary>Uses the real cookie, antiforgery and API pipeline with isolated in-memory persistence.</summary>
internal sealed class ApiFactory(string environment = "Development", bool clinicAdministrator = false, bool staffMember = false) : WebApplicationFactory<Program>
{
    private readonly string _database = Guid.NewGuid().ToString();
    private readonly Patient _patient = DomainTestData.Patient();
    private readonly TimeSlot _slot = DomainTestData.Slot();
    public Guid PatientId => _patient.Id;
    public Guid SlotId => _slot.Id;
    public Guid ClinicId => _slot.ClinicId;
    public Exception? LastError { get; private set; }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment(environment);
        builder.UseSetting("ConnectionStrings:Phcis", "Server=localhost;Database=Tests;Integrated Security=True");
        builder.UseSetting("Development:InitializeDatabase", "false");
        builder.UseSetting("Database:ApplyMigrationsOnStartup", "false");
        builder.UseSetting("Development:SeedClinicsAndSlots", "false");
        builder.UseSetting("Development:SeedAdministrator", "false");
        builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:Phcis"] = "Server=localhost;Database=Tests;Integrated Security=True",
            ["Development:InitializeDatabase"] = "false",
            ["Database:ApplyMigrationsOnStartup"] = "false",
            ["Development:SeedClinicsAndSlots"] = "false",
            ["Development:SeedAdministrator"] = "false",
            ["DatabaseLogging:Enabled"] = "false",
            ["Email:Enabled"] = "false"
        }));
        builder.ConfigureTestServices(services =>
        {
            services.Insert(0, ServiceDescriptor.Singleton<Microsoft.AspNetCore.Diagnostics.IExceptionHandler>(
                new RecordingExceptionHandler(error => LastError = error)));
            services.RemoveAll<PhcisDbContext>();
            services.RemoveAll<DbContextOptions<PhcisDbContext>>();
            services.RemoveAll<IDbContextOptionsConfiguration<PhcisDbContext>>();
            services.AddDbContext<PhcisDbContext>(options => options.UseInMemoryDatabase(_database));
            services.RemoveAll<TimeProvider>();
            services.AddSingleton<TimeProvider>(new TestClock(DomainTestData.Now));
            // CookieContainer uses real wall time; keep cookie expiry on that clock while domain tests remain fixed.
            services.PostConfigure<CookieAuthenticationOptions>(IdentityConstants.ApplicationScheme,
                options => options.TimeProvider = TimeProvider.System);
        });
    }

    public async Task SeedAsync()
    {
        using var scope = Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<PhcisDbContext>();
        context.AddRange(_patient, _slot);
        await context.SaveChangesAsync();
    }

    public HttpClient Client(string? token = null)
    {
        var inner = CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            AllowAutoRedirect = false
        });
        if (token is not null)
            inner.DefaultRequestHeaders.Add("Cookie", "__Host-PHCIS=" + token);
        return new HttpClient(new TestCsrfHandler(inner)) { BaseAddress = new Uri("https://localhost") };
    }

    // Tests of authorization can create protected tickets without depending on account creation.
    // Separate IdentityTests exercise real passwords, registration, login, and logout.
    public string Token(string? patientClaim = null, bool omitPatientClaim = false,
        bool duplicatePatientClaim = false, bool expired = false, bool wrongSignature = false)
    {
        {
            using var scope = Services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<PhcisDbContext>();
            if (!context.Users.Any(x => x.Id == "test-subject"))
            {
                context.Users.Add(new PortalUser
                {
                    Id = "test-subject",
                    UserName = "test-admin@example.test",
                    Email = "test-admin@example.test",
                    NormalizedUserName = "TEST-ADMIN@EXAMPLE.TEST",
                    NormalizedEmail = "TEST-ADMIN@EXAMPLE.TEST",
                    SecurityStamp = Guid.NewGuid().ToString(),
                    PatientId = PatientId
                });
                context.UserRoles.Add(new IdentityUserRole<string> { UserId = "test-subject", RoleId = context.Roles.Single(x => x.Name == PortalRoles.Patient).Id });
                if (clinicAdministrator)
                    context.UserRoles.Add(new IdentityUserRole<string>
                    {
                        UserId = "test-subject",
                        RoleId = context.Roles.Single(x => x.Name == PortalRoles.Admin).Id
                    });
                if (staffMember)
                    context.UserRoles.Add(new IdentityUserRole<string>
                    {
                        UserId = "test-subject",
                        RoleId = context.Roles.Single(x => x.Name == PortalRoles.Staff).Id
                    });
                if (staffMember)
                    context.StaffClinics.Add(new StaffClinic { UserId = "test-subject", ClinicId = ClinicId });
                context.SaveChanges();
            }
        }
        var claims = new List<Claim> { new(ClaimTypes.NameIdentifier, "test-subject") };
        if (!omitPatientClaim)
            claims.Add(new Claim(PatientIdentity.ClaimType, patientClaim ?? PatientId.ToString()));
        if (duplicatePatientClaim)
            claims.Add(new Claim(PatientIdentity.ClaimType, Guid.NewGuid().ToString()));
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, IdentityConstants.ApplicationScheme));
        var ticket = new AuthenticationTicket(principal, new AuthenticationProperties
        {
            IssuedUtc = DateTimeOffset.UtcNow.AddMinutes(-1),
            ExpiresUtc = expired ? DateTimeOffset.UtcNow.AddSeconds(-1) : DateTimeOffset.UtcNow.AddHours(1)
        }, IdentityConstants.ApplicationScheme);
        var cookie = Services.GetRequiredService<IOptionsMonitor<CookieAuthenticationOptions>>()
            .Get(IdentityConstants.ApplicationScheme).TicketDataFormat.Protect(ticket);
        return wrongSignature ? cookie + ".tampered" : cookie;
    }
}

internal sealed class RecordingExceptionHandler(Action<Exception> record) : Microsoft.AspNetCore.Diagnostics.IExceptionHandler
{
    public ValueTask<bool> TryHandleAsync(Microsoft.AspNetCore.Http.HttpContext context, Exception exception, CancellationToken cancellationToken)
    {
        record(exception);
        return ValueTask.FromResult(false);
    }
}

internal sealed class TestCsrfHandler(HttpClient inner) : HttpMessageHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (request.Method != HttpMethod.Get && request.Method != HttpMethod.Head && request.Method != HttpMethod.Options)
        {
            using var csrf = await inner.GetAsync("api/account/csrf", cancellationToken);
            var body = await csrf.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>(cancellationToken);
            request.Headers.Add("X-CSRF-TOKEN", body.GetProperty("requestToken").GetString());
        }
        // The outer HttpClient marks its message as sent. Forward a separate message to the test host.
        using var forwarded = new HttpRequestMessage(request.Method, request.RequestUri);
        foreach (var header in request.Headers)
            forwarded.Headers.TryAddWithoutValidation(header.Key, header.Value);
        if (request.Content is not null)
        {
            forwarded.Content = new ByteArrayContent(await request.Content.ReadAsByteArrayAsync(cancellationToken));
            foreach (var header in request.Content.Headers)
                forwarded.Content.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }
        return await inner.SendAsync(forwarded, cancellationToken);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            inner.Dispose();
        base.Dispose(disposing);
    }
}

