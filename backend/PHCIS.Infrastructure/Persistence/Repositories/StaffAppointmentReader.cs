using Microsoft.EntityFrameworkCore;
using PHCIS.Application.Abstractions;
using PHCIS.Application.Exceptions;
using PHCIS.Application.Models;
using PHCIS.Domain.Entities;

namespace PHCIS.Infrastructure.Persistence.Repositories;

/// <summary>Returns a bounded clinic/day view with patient names, excluding contact and identity details.</summary>
public sealed class StaffAppointmentReader(PhcisDbContext context) : IStaffAppointmentReader
{
    public async Task<StaffAppointmentsPage> ReadAsync(StaffAppointmentsRequest request, DateTimeOffset now, CancellationToken cancellationToken)
    {
        if (!await context.Clinics.AnyAsync(x => x.Id == request.ClinicId, cancellationToken))
            throw new ResourceNotFoundException("Clinic was not found.");
        var from = new DateTimeOffset(request.Date.ToDateTime(TimeOnly.MinValue), TimeSpan.FromHours(2));
        var to = from.AddDays(1);
        var query = context.Appointments.AsNoTracking().Where(x => x.TimeSlot.ClinicId == request.ClinicId &&
            x.TimeSlot.StartsAt >= from && x.TimeSlot.StartsAt < to);
        if (request.Status is not null)
        {
            var status = Enum.Parse<AppointmentStatus>(request.Status);
            query = query.Where(x => x.Status == status);
        }
        var rows = await query.OrderBy(x => x.TimeSlot.StartsAt).ThenBy(x => x.Id)
            .Skip(request.Offset).Take(request.Limit + 1)
            .Select(x => new StaffAppointmentRow(x.Id, x.Patient.FirstName + " " + x.Patient.LastName,
                x.TimeSlot.StartsAt, x.TimeSlot.EndsAt, x.Status.ToString(),
                x.Status == AppointmentStatus.Scheduled && x.TimeSlot.EndsAt <= now))
            .ToListAsync(cancellationToken);
        return new StaffAppointmentsPage(rows.Take(request.Limit).ToArray(), request.Offset, request.Limit, rows.Count > request.Limit);
    }
}
