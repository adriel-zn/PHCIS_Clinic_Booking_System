using FluentValidation;
using PHCIS.Application.Abstractions;
using PHCIS.Domain.Entities;

namespace PHCIS.Application.Services;

/// <summary>Validates clinic details and creates future availability in South African time.</summary>
public sealed class ClinicManagementService(IClinicWriter writer, TimeProvider clock,
    IValidator<CreateClinicRequest> validator) : IClinicManagementService
{
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
