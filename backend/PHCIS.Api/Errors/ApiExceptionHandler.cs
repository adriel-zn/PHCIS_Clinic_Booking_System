using FluentValidation;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using PHCIS.Application.Exceptions;
using PHCIS.Domain.Exceptions;

namespace PHCIS.Api.Errors;

/// <summary>Maps application failures to HTTP without leaking persistence details or patient payloads.</summary>
public sealed class ApiExceptionHandler(ILogger<ApiExceptionHandler> logger) : IExceptionHandler
{
    /// <inheritdoc />
    public async ValueTask<bool> TryHandleAsync(
        HttpContext context,
        Exception exception,
        CancellationToken cancellationToken)
    {
        if (exception is OperationCanceledException && context.RequestAborted.IsCancellationRequested)
        {
            // A disconnected client cannot receive a Problem Details response.
            context.Response.StatusCode = 499;
            return true;
        }

        var status = exception switch
        {
            ValidationException => StatusCodes.Status400BadRequest,
            ResourceNotFoundException => StatusCodes.Status404NotFound,
            AppointmentConflictException or DomainRuleException or Microsoft.EntityFrameworkCore.DbUpdateConcurrencyException => StatusCodes.Status409Conflict,
            _ => StatusCodes.Status500InternalServerError
        };

        ProblemDetails details;
        if (exception is ValidationException validation)
        {
            var errors = validation.Errors
                .GroupBy(error => error.PropertyName)
                .ToDictionary(group => group.Key, group => group.Select(error => error.ErrorMessage).Distinct().ToArray());
            details = new ValidationProblemDetails(errors)
            {
                Title = "Request validation failed."
            };
        }
        else
        {
            details = new ProblemDetails
            {
                Title = status switch
                {
                    404 => "Resource not found.",
                    409 => "The requested operation conflicts with the current state.",
                    _ => "An unexpected error occurred."
                },
                Detail = status == 500 ? "Contact support with the trace identifier." : exception is Microsoft.EntityFrameworkCore.DbUpdateConcurrencyException
                    ? "The record changed. Refresh before trying again." : exception.Message
            };
        }

        if (status == 500)
        {
            // Do not log raw exceptions: database errors may contain credentials or patient data.
            logger.LogError("Unhandled {ExceptionType}. Trace identifier: {TraceId}",
                exception.GetType().Name, context.TraceIdentifier);
        }

        details.Status = status;
        if (status != 500)
            logger.LogWarning("Rejected operation {ExceptionType}. Trace identifier: {TraceId}", exception.GetType().Name, context.TraceIdentifier);
        context.Response.StatusCode = status;
        await ApiProblemResponse.WriteAsync(context, details);
        return true;
    }
}

