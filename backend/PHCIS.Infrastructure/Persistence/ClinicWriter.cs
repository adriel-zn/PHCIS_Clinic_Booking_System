using System.Data;
using Microsoft.EntityFrameworkCore;
using PHCIS.Application.Abstractions;
using PHCIS.Domain.Entities;
using PHCIS.Domain.Exceptions;
using PHCIS.Application.Exceptions;
using PHCIS.Application.Services;

namespace PHCIS.Infrastructure.Persistence;

/// <summary>Uses one transaction for clinic creation, duplicate checking, and initial availability.</summary>
public sealed class ClinicWriter(PhcisDbContext context) : IClinicWriter
{
    public Task UpdateAsync(Guid clinicId, string name, string address, string? phoneNumber, CancellationToken cancellationToken) =>
        WriteAsync(async () =>
        {
            var clinic = await context.Clinics.SingleOrDefaultAsync(x => x.Id == clinicId, cancellationToken)
                ?? throw new ResourceNotFoundException("Clinic was not found.");
            if (await context.Clinics.AnyAsync(x => x.Id != clinicId && x.Name.ToUpper() == name.ToUpper(), cancellationToken))
                throw new DomainRuleException("A clinic with this name already exists.");
            clinic.UpdateDetails(name, address, phoneNumber);
        }, cancellationToken);

    public async Task<IReadOnlyList<ManagedTimeSlot>> SlotsAsync(Guid clinicId, DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken)
    {
        if (!await context.Clinics.AnyAsync(x => x.Id == clinicId, cancellationToken))
            throw new ResourceNotFoundException("Clinic was not found.");
        return await context.TimeSlots.AsNoTracking()
            .Where(x => x.ClinicId == clinicId && x.StartsAt >= from && x.StartsAt < to)
            .OrderBy(x => x.StartsAt).ThenBy(x => x.Id).Take(1000)
            .Select(x => new ManagedTimeSlot(x.Id, x.StartsAt, x.EndsAt, x.IsBlocked,
                context.Appointments.Any(a => a.TimeSlotId == x.Id && a.Status != AppointmentStatus.Cancelled),
                context.Appointments.Any(a => a.TimeSlotId == x.Id)))
            .ToListAsync(cancellationToken);
    }

    public async Task<Guid> SaveSlotAsync(Guid clinicId, Guid? slotId, DateTimeOffset start, DateTimeOffset end, DateTimeOffset now, CancellationToken cancellationToken)
    {
        Guid result = default;
        await WriteAsync(async () =>
        {
            var clinic = await context.Clinics.SingleOrDefaultAsync(x => x.Id == clinicId, cancellationToken)
                ?? throw new ResourceNotFoundException("Clinic was not found.");
            TimeSlot slot;
            if (slotId is not null)
            {
                slot = await FindSlotAsync(clinicId, slotId.Value, cancellationToken);
                if (slot.StartsAt <= now || await context.Appointments.AnyAsync(a => a.TimeSlotId == slot.Id, cancellationToken))
                    throw new DomainRuleException("Slots with appointment history or a past start cannot be rescheduled. Add a new slot instead.");
            }
            else
                slot = new TimeSlot(clinic, start, end);
            if (await context.TimeSlots.AnyAsync(x => x.ClinicId == clinicId && x.Id != slot.Id &&
                x.StartsAt < end && start < x.EndsAt, cancellationToken))
                throw new DomainRuleException("This schedule overlaps another clinic time slot. Edit or reopen the existing slot instead.");
            slot.UpdateSchedule(start, end);
            clinic.MarkScheduleManaged();
            if (slotId is null)
                context.TimeSlots.Add(slot);
            result = slot.Id;
        }, cancellationToken);
        return result;
    }

    public Task SetBlockedAsync(Guid clinicId, Guid slotId, bool blocked, DateTimeOffset now, CancellationToken cancellationToken) =>
        WriteAsync(async () =>
        {
            var slot = await FindSlotAsync(clinicId, slotId, cancellationToken);
            if (slot.StartsAt <= now)
                throw new DomainRuleException("Past slots cannot be closed or reopened.");
            if (await context.Appointments.AnyAsync(a => a.TimeSlotId == slotId && a.Status != AppointmentStatus.Cancelled, cancellationToken))
                throw new DomainRuleException("Booked slots cannot be changed. Manage the appointment first.");
            slot.SetBlocked(blocked);
            var clinic = await context.Clinics.SingleAsync(x => x.Id == clinicId, cancellationToken);
            clinic.MarkScheduleManaged();
        }, cancellationToken);

    private async Task<TimeSlot> FindSlotAsync(Guid clinicId, Guid slotId, CancellationToken cancellationToken) =>
        await context.TimeSlots.SingleOrDefaultAsync(x => x.Id == slotId && x.ClinicId == clinicId, cancellationToken)
            ?? throw new ResourceNotFoundException("Time slot was not found for this clinic.");

    private async Task WriteAsync(Func<Task> change, CancellationToken cancellationToken)
    {
        try
        {
            await context.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
            {
                await using var transaction = context.Database.IsRelational()
                    ? await context.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken) : null;
                await change();
                await context.SaveChangesAsync(cancellationToken);
                if (transaction is not null)
                    await transaction.CommitAsync(cancellationToken);
            });
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new AppointmentConflictException("The clinic or slot changed. Refresh before trying again.");
        }
        catch (DbUpdateException error) when (error.InnerException is Microsoft.Data.SqlClient.SqlException { Number: 2601 or 2627 })
        {
            throw new AppointmentConflictException("The clinic or time slot conflicts with an existing record.");
        }
    }

    public async Task CreateAsync(Clinic clinic, IReadOnlyCollection<TimeSlot> slots, CancellationToken cancellationToken)
    {
        await context.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            // Serializable prevents concurrent creations from passing the same duplicate check.
            await using var transaction = context.Database.IsRelational()
                ? await context.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken) : null;
            if (await context.Clinics.AnyAsync(x => x.Name.ToUpper() == clinic.Name.ToUpper(), cancellationToken))
            {
                throw new DomainRuleException("A clinic with this name already exists.");
            }
            context.Clinics.Add(clinic);
            context.TimeSlots.AddRange(slots);
            await context.SaveChangesAsync(cancellationToken);
            if (transaction is not null)
                await transaction.CommitAsync(cancellationToken);
        });
    }
}
