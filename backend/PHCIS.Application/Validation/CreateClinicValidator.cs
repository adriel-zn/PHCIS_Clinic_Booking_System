using FluentValidation;
using PHCIS.Application.Services;

namespace PHCIS.Application.Validation;

public sealed class CreateClinicValidator : AbstractValidator<CreateClinicRequest>
{
    public CreateClinicValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Address).NotEmpty().MaximumLength(500);
        RuleFor(x => x.PhoneNumber).MaximumLength(30);
    }
}
