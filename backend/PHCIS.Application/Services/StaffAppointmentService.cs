using FluentValidation;
using PHCIS.Application.Abstractions;
using PHCIS.Application.Exceptions;
using PHCIS.Application.Models;

namespace PHCIS.Application.Services;

/// <summary>Staff lifecycle use cases reuse Domain rules and the existing concurrency-aware unit of work.</summary>
public sealed class StaffAppointmentService(IStaffAppointmentReader reader, IAppointmentRepository appointments,
    IUnitOfWork unitOfWork, IValidator<StaffAppointmentsRequest> validator, TimeProvider clock) : IStaffAppointmentService
{
    public async Task<StaffAppointmentsPage> ListAsync(StaffAppointmentsRequest request, CancellationToken cancellationToken)
    {
        await validator.ValidateAndThrowAsync(request, cancellationToken);
        return await reader.ReadAsync(request, clock.GetUtcNow(), cancellationToken);
    }

    public Task CompleteAsync(Guid clinicId, Guid appointmentId, CancellationToken cancellationToken) =>
        FinalizeAsync(clinicId, appointmentId, true, cancellationToken);

    public Task MarkNoShowAsync(Guid clinicId, Guid appointmentId, CancellationToken cancellationToken) =>
        FinalizeAsync(clinicId, appointmentId, false, cancellationToken);

    private async Task FinalizeAsync(Guid clinicId, Guid appointmentId, bool completed, CancellationToken cancellationToken)
    {
        var appointment = await appointments.FindWithSlotAsync(appointmentId, cancellationToken);
        if (appointment is null || appointment.TimeSlot.ClinicId != clinicId)
            throw new ResourceNotFoundException("Appointment was not found for the selected clinic.");
        if (completed)
            appointment.Complete(clock);
        else
            appointment.MarkNoShow(clock);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
