namespace PHCIS.Web.Services;

/// <summary>A safe display message with optional field errors and an API support trace.</summary>
public sealed class AppointmentApiException(
    int statusCode,
    string message,
    string? traceId = null,
    IReadOnlyDictionary<string, string[]>? errors = null) : Exception(message)
{
    public int StatusCode { get; } = statusCode;
    public string? TraceId { get; } = traceId;
    public IReadOnlyDictionary<string, string[]> Errors { get; } = errors ?? new Dictionary<string, string[]>();
}

