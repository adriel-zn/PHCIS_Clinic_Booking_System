using PHCIS.Application;
using PHCIS.Api;
using PHCIS.Api.Errors;
using PHCIS.Infrastructure;
using Serilog;

var initializeOnly = args.Contains("--initialize-development");
var builder = WebApplication.CreateBuilder(args.Where(arg => arg != "--initialize-development").ToArray());
builder.Services.AddSerilog(configuration => configuration
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console());
builder.Services.AddApplication(builder.Configuration);
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddApi(builder.Configuration);
var app = builder.Build();
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

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapControllers();
app.Run();

/// <summary>API entry point exposed for in-process HTTP integration testing.</summary>
public partial class Program { }
