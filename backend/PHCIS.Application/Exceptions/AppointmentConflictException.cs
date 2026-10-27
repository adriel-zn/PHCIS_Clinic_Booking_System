namespace PHCIS.Application.Exceptions;

/// <summary>The requested write conflicts with occupied slots or concurrently modified data.</summary>
public sealed class AppointmentConflictException(string message) : Exception(message);

