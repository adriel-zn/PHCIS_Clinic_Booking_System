using FluentValidation;
using PHCIS.Domain.Entities;

namespace PHCIS.Application.Validation;

/// <summary>
/// Validates the basic fields of a TimeSlot; booking policies are handled separately.
/// </summary>
public sealed class TimeSlotValidator : AbstractValidator<TimeSlot>
{
    public TimeSlotValidator()
    {
        RuleFor(x => x.ClinicId)
            .NotEmpty();
        RuleFor(x => x.StartsAt)
            .NotEmpty();
        RuleFor(x => x.EndsAt)
            .GreaterThan(x => x.StartsAt);
    }
}
