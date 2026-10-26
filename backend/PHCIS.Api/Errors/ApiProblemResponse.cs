using Microsoft.AspNetCore.Mvc;

namespace PHCIS.Api.Errors;

/// <summary>Keeps error responses consistent even when a client requests an unsupported media type.</summary>
internal static class ApiProblemResponse
{
    public static async Task WriteAsync(HttpContext context, ProblemDetails details)
    {
        details.Status ??= context.Response.StatusCode;
        details.Instance = context.Request.Path;
        details.Extensions["traceId"] = context.TraceIdentifier;
        context.Response.StatusCode = details.Status.Value;
        var service = context.RequestServices.GetRequiredService<IProblemDetailsService>();

        if (!await service.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = context,
            ProblemDetails = details
        }))
        {
            await context.Response.WriteAsJsonAsync(
                details,
                options: (System.Text.Json.JsonSerializerOptions?)null,
                contentType: "application/problem+json",
                cancellationToken: context.RequestAborted);
        }
    }
}

