using PHCIS.Application;
using PHCIS.Api;
using PHCIS.Api.Errors;
using PHCIS.Infrastructure;
using Serilog;
using PHCIS.Api.Observability;
using Microsoft.AspNetCore.DataProtection;

var initializeOnly = args.Contains("--initialize-development");
var builder = WebApplication.CreateBuilder(args.Where(arg => arg != "--initialize-development").ToArray());
if (builder.Configuration.GetValue<bool>("ReverseProxy:Enabled"))
{
    builder.Services.Configure<Microsoft.AspNetCore.Builder.ForwardedHeadersOptions>(options =>
    {
        options.ForwardedHeaders = Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedFor | Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedProto;
        options.KnownIPNetworks.Add(System.Net.IPNetwork.Parse("172.28.0.0/24"));
    });
}
if (!string.IsNullOrWhiteSpace(builder.Configuration["DataProtection:KeyPath"]))
    builder.Services.AddDataProtection().PersistKeysToFileSystem(new DirectoryInfo(builder.Configuration["DataProtection:KeyPath"]!));
builder.Services.AddSingleton<DatabaseLogQueue>();
builder.Services.AddHostedService<DatabaseLogWriter>();
builder.Services.AddSerilog((services, configuration) =>
{
    configuration
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console();
    if (builder.Configuration.GetValue<bool>("DatabaseLogging:Enabled"))
        configuration.WriteTo.Sink(services.GetRequiredService<DatabaseLogQueue>());
});
builder.Services.AddApplication(builder.Configuration);
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddApi(builder.Configuration);
var app = builder.Build();
app.UseForwardedHeaders();
await app.ApplyMigrationsAsync();
await PHCIS.Api.Authentication.IdentityRoleInitialization.InitializeAsync(app);
await PHCIS.Api.Development.DevelopmentData.InitializeAsync(app);
await PHCIS.Api.Development.ClinicDevelopmentData.InitializeAsync(app);
await PHCIS.Api.Development.AdministratorDevelopmentData.InitializeAsync(app);
if (initializeOnly)
{
    if (!app.Environment.IsDevelopment())
    {
        throw new InvalidOperationException("Development initialization requires the Development environment.");
    }
    return;
}

app.UseSerilogRequestLogging(options =>
{
    options.EnrichDiagnosticContext = (diagnostics, http) =>
    {
        diagnostics.Set("TraceId", http.TraceIdentifier);
        diagnostics.Set("ActorId", http.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? "anonymous");
        diagnostics.Set("Route", (http.GetEndpoint() as Microsoft.AspNetCore.Routing.RouteEndpoint)?.RoutePattern.RawText ?? "unmatched");
    };
});
app.UseExceptionHandler();
app.UseStatusCodePages(async statusContext =>
{
    var context = statusContext.HttpContext;
    await ApiProblemResponse.WriteAsync(context, new Microsoft.AspNetCore.Mvc.ProblemDetails
    {
        Status = context.Response.StatusCode
    });
});
app.UseHttpsRedirection();
app.UseCors("Frontend");
app.UseAuthentication();
app.UseAuthorization();
app.Use(async (http, next) =>
{
    if (http.Request.Path.StartsWithSegments("/graphql") && HttpMethods.IsPost(http.Request.Method))
    {
        try
        { await http.RequestServices.GetRequiredService<Microsoft.AspNetCore.Antiforgery.IAntiforgery>().ValidateRequestAsync(http); }
        catch (Microsoft.AspNetCore.Antiforgery.AntiforgeryValidationException)
        {
            http.Response.StatusCode = 400;
            await ApiProblemResponse.WriteAsync(http, new Microsoft.AspNetCore.Mvc.ProblemDetails { Status = 400, Title = "Invalid CSRF token." });
            return;
        }
    }
    await next(http);
});

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapControllers();
app.MapGet("/health", () => Results.Ok(new { Status = "Ready" })).AllowAnonymous();
app.MapHub<PHCIS.Api.Realtime.AppointmentHub>("/hubs/appointments");
app.MapGraphQL("/graphql").WithOptions(new HotChocolate.AspNetCore.GraphQLServerOptions
{
    EnableGetRequests = false,
    Tool = { Enable = false }
}).RequireAuthorization(PHCIS.Api.Authentication.PatientIdentity.Policy);
app.Run();

/// <summary>API entry point exposed for in-process HTTP integration testing.</summary>
public partial class Program { }
