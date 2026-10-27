using FluentValidation;
using PHCIS.Application.Models;

namespace PHCIS.Application.Validation;

public sealed class StaffAppointmentsValidator : AbstractValidator<StaffAppointmentsRequest>
{
    public StaffAppointmentsValidator()
    {
        RuleFor(x => x.ClinicId).NotEmpty();
        RuleFor(x => x.Date).NotEmpty().LessThan(new DateOnly(9999, 12, 31));
        RuleFor(x => x.Offset).GreaterThanOrEqualTo(0);
        RuleFor(x => x.Limit).InclusiveBetween(1, 100);
        RuleFor(x => x.Status).Must(status => status is null or "Scheduled" or "Completed" or "NoShow" or "Cancelled")
            .WithMessage("Choose a valid appointment status.");
    }
}
