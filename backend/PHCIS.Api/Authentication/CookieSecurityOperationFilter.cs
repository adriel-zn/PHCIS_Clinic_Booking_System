using Microsoft.AspNetCore.Authorization;
using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace PHCIS.Api.Authentication;

/// <summary>Documents session requirements and CSRF headers only where they apply.</summary>
public sealed class CookieSecurityOperationFilter : IOperationFilter
{
    /// <inheritdoc />
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        var anonymous = context.MethodInfo.IsDefined(typeof(AllowAnonymousAttribute), true);
        operation.Security = [];
        var requirement = new OpenApiSecurityRequirement();
        if (!anonymous)
        {
            requirement[new OpenApiSecuritySchemeReference("session", context.Document)] = [];
        }
        if (context.ApiDescription.HttpMethod is not ("GET" or "HEAD" or "OPTIONS"))
        {
            requirement[new OpenApiSecuritySchemeReference("csrf", context.Document)] = [];
        }
        if (requirement.Count > 0)
        {
            operation.Security.Add(requirement);
        }
    }
}

