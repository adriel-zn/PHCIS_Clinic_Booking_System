using Microsoft.EntityFrameworkCore;
using PHCIS.Application.Abstractions;
using PHCIS.Application.Models;

namespace PHCIS.Infrastructure.Persistence.Repositories;

public sealed class PortalReader(PhcisDbContext context) : IPortalReader
{
    public Task<TimeSlotDto?> SlotAsync(Guid clinicId, Guid slotId, CancellationToken cancellationToken) =>
        context.TimeSlots.AsNoTracking().Where(x => x.Id == slotId && x.ClinicId == clinicId)
            .Select(x => new TimeSlotDto(x.Id, x.ClinicId, x.StartsAt, x.EndsAt)).SingleOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyList<ClinicCard>> ClinicsAsync(CancellationToken cancellationToken) =>
        await context.Clinics.AsNoTracking().OrderBy(x => x.Name)
            .Select(x => new ClinicCard(x.Id, x.Name, x.Address, x.PhoneNumber)).ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<AppointmentDetails>> AppointmentsAsync(Guid patientId, CancellationToken cancellationToken) =>
        await context.Appointments.AsNoTracking().Where(x => x.PatientId == patientId)
            .OrderBy(x => x.TimeSlot.StartsAt)
            .Select(x => new AppointmentDetails(x.Id, x.TimeSlotId, x.TimeSlot.ClinicId,
                x.TimeSlot.Clinic.Name, x.TimeSlot.Clinic.Address, x.TimeSlot.StartsAt, x.TimeSlot.EndsAt,
                x.Status.ToString(), x.ReasonForVisit)).ToListAsync(cancellationToken);

    public Task<PatientProfile?> ProfileAsync(Guid patientId, CancellationToken cancellationToken) =>
        context.Patients.AsNoTracking().Where(x => x.Id == patientId)
            .Select(x => new PatientProfile(x.Id, x.FirstName, x.LastName, x.Email, x.PhoneNumber,
                x.DateOfBirth, x.NationalId)).SingleOrDefaultAsync(cancellationToken);
}

