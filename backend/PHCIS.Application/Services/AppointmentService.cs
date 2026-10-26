using AutoMapper;
using FluentValidation;
using PHCIS.Application.Abstractions;
using PHCIS.Application.Exceptions;
using PHCIS.Application.Models;
using PHCIS.Domain.Entities;

namespace PHCIS.Application.Services;

/// <summary>
/// Coordinates input validation and persistence. Lifecycle rules remain in Domain;
/// the unit of work and SQL Server constraints protect concurrent writes.
/// </summary>
public sealed class AppointmentService(
    IPatientReader patients,
    ITimeSlotRepository slots,
    IAppointmentRepository appointments,
    IUnitOfWork unitOfWork,
    IValidator<BookAppointmentRequest> bookingValidator,
    IValidator<CancelAppointmentRequest> cancellationValidator,
    IValidator<RescheduleAppointmentRequest> reschedulingValidator,
    IMapper mapper,
    TimeProvider timeProvider, IAppointmentEffects? effects = null) : IAppointmentService
{
    public async Task<AppointmentDto> BookAsync(
        BookAppointmentRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        await bookingValidator.ValidateAndThrowAsync(request, cancellationToken);

        var patient = await patients.FindAsync(request.PatientId, cancellationToken)
            ?? throw new ResourceNotFoundException("Patient was not found.");
        var slot = await slots.FindAsync(request.TimeSlotId, cancellationToken)
            ?? throw new ResourceNotFoundException("Time slot was not found.");

        if (await appointments.IsSlotOccupiedAsync(slot.Id, cancellationToken))
        {
            throw new AppointmentConflictException("The time slot is no longer available.");
        }

        // This precheck improves feedback; the unique index remains authoritative under races.
        var appointment = new Appointment(patient, slot, timeProvider, request.ReasonForVisit);
        appointments.Add(appointment);
        effects?.Stage(appointment);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        if (effects is not null)
            await effects.CommittedAsync(appointment, cancellationToken);

        return mapper.Map<AppointmentDto>(appointment);
    }

    public async Task<AppointmentDto> CancelAsync(
        CancelAppointmentRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        await cancellationValidator.ValidateAndThrowAsync(request, cancellationToken);

        var appointment = await appointments.FindWithSlotAsync(request.AppointmentId, cancellationToken);

        // Use the same response for missing and out-of-scope records.
        if (appointment is null || appointment.PatientId != request.PatientId)
        {
            throw new ResourceNotFoundException("Appointment was not found for the patient.");
        }

        appointment.Cancel(request.Reason, timeProvider);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        if (effects is not null)
            await effects.CommittedAsync(appointment, cancellationToken);

        return mapper.Map<AppointmentDto>(appointment);
    }

    public async Task<AppointmentDto> RescheduleAsync(RescheduleAppointmentRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        await reschedulingValidator.ValidateAndThrowAsync(request, cancellationToken);
        var appointment = await appointments.FindWithSlotAsync(request.AppointmentId, cancellationToken);
        if (appointment is null || appointment.PatientId != request.PatientId)
        {
            throw new ResourceNotFoundException("Appointment was not found for the patient.");
        }
        var slot = await slots.FindAsync(request.TimeSlotId, cancellationToken)
            ?? throw new ResourceNotFoundException("Time slot was not found.");
        if (slot.Id != appointment.TimeSlotId && await appointments.IsSlotOccupiedAsync(slot.Id, cancellationToken))
        {
            throw new AppointmentConflictException("The replacement slot is no longer available.");
        }
        appointment.Reschedule(slot, timeProvider);
        effects?.Stage(appointment);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        if (effects is not null)
            await effects.CommittedAsync(appointment, cancellationToken);
        return mapper.Map<AppointmentDto>(appointment);
    }
}

