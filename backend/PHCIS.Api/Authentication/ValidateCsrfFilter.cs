using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace PHCIS.Api.Authentication;

/// <summary>Protects cookie-authenticated writes, including login and registration, without enabling MVC views.</summary>
public sealed class ValidateCsrfFilter(IAntiforgery antiforgery) : IAsyncAuthorizationFilter
{
    /// <inheritdoc />
    public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        var request = context.HttpContext.Request;
        if (HttpMethods.IsGet(request.Method) || HttpMethods.IsHead(request.Method) || HttpMethods.IsOptions(request.Method))
        {
            return;
        }
        try
        {
            await antiforgery.ValidateRequestAsync(context.HttpContext);
        }
        catch (AntiforgeryValidationException)
        {
            var details = new ValidationProblemDetails(new Dictionary<string, string[]>
            {
                ["Session"] = ["Your session could not be verified. Refresh the page and try again."]
            })
            {
                Status = 400,
                Title = "Request verification failed.",
                Instance = request.Path
            };
            details.Extensions["traceId"] = context.HttpContext.TraceIdentifier;
            context.Result = new ObjectResult(details) { StatusCode = 400, ContentTypes = { "application/problem+json" } };
        }
    }
}

