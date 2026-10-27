using FluentValidation;
using PHCIS.Domain.Entities;

namespace PHCIS.Application.Validation;

/// <summary>
/// Validates the basic fields of a Appointment; booking policies are handled separately.
/// </summary>
public sealed class AppointmentValidator : AbstractValidator<Appointment>
{
    public AppointmentValidator()
    {
        RuleFor(x => x.PatientId)
            .NotEmpty();
        RuleFor(x => x.TimeSlotId)
            .NotEmpty();
        RuleFor(x => x.Status)
            .IsInEnum();
        RuleFor(x => x.CreatedAt)
            .NotEmpty();
    }
}
