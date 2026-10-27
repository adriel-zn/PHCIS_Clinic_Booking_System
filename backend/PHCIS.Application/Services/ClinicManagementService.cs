using FluentValidation;
using PHCIS.Application.Abstractions;
using PHCIS.Domain.Entities;
using PHCIS.Domain.Exceptions;

namespace PHCIS.Application.Services;

/// <summary>Validates clinic details and creates future availability in South African time.</summary>
public sealed class ClinicManagementService(IClinicWriter writer, TimeProvider clock,
    IValidator<CreateClinicRequest> validator) : IClinicManagementService
{
    public async Task UpdateAsync(Guid clinicId, CreateClinicRequest request, CancellationToken cancellationToken)
    {
        await validator.ValidateAndThrowAsync(request, cancellationToken);
        await writer.UpdateAsync(clinicId, request.Name.Trim(), request.Address.Trim(), request.PhoneNumber?.Trim(), cancellationToken);
    }

    public Task<IReadOnlyList<ManagedTimeSlot>> SlotsAsync(Guid clinicId, DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken)
    {
        if (from == default || to <= from || to - from > TimeSpan.FromDays(31))
            throw new DomainRuleException("Choose a time-slot interval of up to 31 days.");
        return writer.SlotsAsync(clinicId, from, to, cancellationToken);
    }

    public Task<Guid> AddSlotAsync(Guid clinicId, SlotScheduleRequest request, CancellationToken cancellationToken)
    {
        ValidateSchedule(request);
        return writer.SaveSlotAsync(clinicId, null, request.StartsAt, request.EndsAt, clock.GetUtcNow(), cancellationToken);
    }

    public async Task UpdateSlotAsync(Guid clinicId, Guid slotId, SlotScheduleRequest request, CancellationToken cancellationToken)
    {
        ValidateSchedule(request);
        await writer.SaveSlotAsync(clinicId, slotId, request.StartsAt, request.EndsAt, clock.GetUtcNow(), cancellationToken);
    }

    public Task SetBlockedAsync(Guid clinicId, Guid slotId, bool blocked, CancellationToken cancellationToken) =>
        writer.SetBlockedAsync(clinicId, slotId, blocked, clock.GetUtcNow(), cancellationToken);

    private void ValidateSchedule(SlotScheduleRequest request)
    {
        if (request.StartsAt <= clock.GetUtcNow() || request.EndsAt <= request.StartsAt ||
            request.EndsAt - request.StartsAt > TimeSpan.FromHours(8))
            throw new DomainRuleException("Choose a future slot ending after it starts, lasting at most eight hours.");
    }

    public async Task<Guid> CreateAsync(CreateClinicRequest request, CancellationToken cancellationToken)
    {
        await validator.ValidateAndThrowAsync(request, cancellationToken);
        var clinic = new Clinic(request.Name, request.Address, request.PhoneNumber);
        var now = clock.GetUtcNow();
        var today = DateOnly.FromDateTime(now.ToOffset(TimeSpan.FromHours(2)).DateTime);
        var slots = new List<TimeSlot>();
        for (var day = 0; day < 15; day++)
        {
            var date = today.AddDays(day);
            if (date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday)
                continue;
            for (var minute = 8 * 60; minute < 17 * 60; minute += 30)
            {
                var start = new DateTimeOffset(date.ToDateTime(new TimeOnly(minute / 60, minute % 60)), TimeSpan.FromHours(2));
                if (start > now)
                    slots.Add(new TimeSlot(clinic, start, start.AddMinutes(30)));
            }
        }
        await writer.CreateAsync(clinic, slots, cancellationToken);
        return clinic.Id;
    }
}
