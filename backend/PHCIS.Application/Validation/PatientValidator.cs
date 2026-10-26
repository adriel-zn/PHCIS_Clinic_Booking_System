using FluentValidation;
using PHCIS.Domain.Entities;

namespace PHCIS.Application.Validation;

/// <summary>
/// Validates the basic fields of a Patient; booking policies are handled separately.
/// </summary>
public sealed class PatientValidator : AbstractValidator<Patient>
{
    public PatientValidator(TimeProvider timeProvider)
    {
        RuleFor(x => x.FirstName)
            .NotEmpty()
            .MaximumLength(100);
        RuleFor(x => x.LastName)
            .NotEmpty()
            .MaximumLength(100);
        RuleFor(x => x.DateOfBirth)
            .NotEmpty()
            .Must(date => date <= DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime))
            .WithMessage("Date of birth must not be in the future.");
        RuleFor(x => x.PhoneNumber)
            .MaximumLength(30);
        RuleFor(x => x.Email)
            .MaximumLength(254)
            .EmailAddress()
            .When(x => !string.IsNullOrEmpty(x.Email));
    }
}
