using FluentValidation;
using PHCIS.Application.Models;

namespace PHCIS.Application.Validation;

/// <summary>Validates cancellation shape; timing and status are enforced by Domain.</summary>
public sealed class CancelAppointmentRequestValidator : AbstractValidator<CancelAppointmentRequest>
{
    public CancelAppointmentRequestValidator()
    {
        RuleFor(x => x.AppointmentId).NotEmpty();
        RuleFor(x => x.PatientId).NotEmpty();
        RuleFor(x => x.Reason)
            .NotEmpty()
            .Must(reason => reason is null || reason.Trim().Length <= 500)
            .WithMessage("Cancellation reason must not exceed 500 characters after trimming.");
    }
}

