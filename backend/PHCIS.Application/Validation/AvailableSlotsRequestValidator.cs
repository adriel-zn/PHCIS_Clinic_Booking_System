using FluentValidation;
using PHCIS.Application.Models;

namespace PHCIS.Application.Validation;

/// <summary>Requires a valid interval and bounds each page to 100 slots.</summary>
public sealed class AvailableSlotsRequestValidator : AbstractValidator<AvailableSlotsRequest>
{
    public AvailableSlotsRequestValidator()
    {
        RuleFor(x => x.ClinicId).NotEmpty();
        RuleFor(x => x.From).NotEmpty();
        RuleFor(x => x.To).GreaterThan(x => x.From);
        RuleFor(x => x.Offset).GreaterThanOrEqualTo(0);
        RuleFor(x => x.Limit).InclusiveBetween(1, 100);
    }
}

