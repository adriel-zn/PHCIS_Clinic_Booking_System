namespace PHCIS.Infrastructure.Persistence;

public sealed class ActivityLog
{
    public long Id { get; set; }
    public DateTimeOffset Timestamp { get; set; }
    public string Level { get; set; } = "";
    public string Event { get; set; } = "";
    public string Properties { get; set; } = "{}";
}

public sealed class ErrorLog
{
    public long Id { get; set; }
    public DateTimeOffset Timestamp { get; set; }
    public string Level { get; set; } = "";
    public string Event { get; set; } = "";
    public string Properties { get; set; } = "{}";
    public string? ExceptionType { get; set; }
}

/// <summary>Durable email work staged in the same transaction as the appointment.</summary>
public sealed class AppointmentNotification
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid AppointmentId { get; set; }
    public string Kind { get; set; } = "Confirmation";
    public DateTimeOffset SlotStartsAt { get; set; }
    public Guid Generation { get; set; }
    public DateTimeOffset DueAt { get; set; }
    public DateTimeOffset? ProcessedAt { get; set; }
    public DateTimeOffset? LeaseUntil { get; set; }
    public int Attempts { get; set; }
    public string Status { get; set; } = "Pending";
}
