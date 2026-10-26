using FluentValidation;
using PHCIS.Domain.Entities;

namespace PHCIS.Application.Validation;

/// <summary>
/// Validates the basic fields of a Clinic; booking policies are handled separately.
/// </summary>
public sealed class ClinicValidator : AbstractValidator<Clinic>
{
    public ClinicValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .MaximumLength(200);
        RuleFor(x => x.Address)
            .NotEmpty()
            .MaximumLength(500);
        RuleFor(x => x.PhoneNumber)
            .MaximumLength(30);
    }
}
