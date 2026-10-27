namespace PHCIS.Application.Models;

public sealed record StaffAppointmentsRequest(Guid ClinicId, DateOnly Date, string? Status = null, int Offset = 0, int Limit = 50);
public sealed record StaffAppointmentRow(Guid Id, string PatientName, DateTimeOffset StartsAt,
    DateTimeOffset EndsAt, string Status, bool CanFinalize);
public sealed record StaffAppointmentsPage(IReadOnlyList<StaffAppointmentRow> Items, int Offset, int Limit, bool HasMore);
