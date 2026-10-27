namespace PHCIS.Application.Exceptions;

/// <summary>An application resource is missing or is outside the requested patient scope.</summary>
public sealed class ResourceNotFoundException(string message) : Exception(message);

