namespace PHCIS.Web.Models;

public sealed record StaffDashboardAccess(bool CanViewAppointments);
public sealed record StaffAppointmentRow(Guid Id, string PatientName, DateTimeOffset StartsAt,
    DateTimeOffset EndsAt, string Status, bool CanFinalize);
public sealed record StaffAppointmentsPage(IReadOnlyList<StaffAppointmentRow> Items, int Offset, int Limit, bool HasMore);
