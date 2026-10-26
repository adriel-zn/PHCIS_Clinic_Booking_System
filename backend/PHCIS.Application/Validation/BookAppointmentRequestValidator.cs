using FluentValidation;
using PHCIS.Application.Models;

namespace PHCIS.Application.Validation;

/// <summary>Validates identifiers before any persistence calls.</summary>
public sealed class BookAppointmentRequestValidator : AbstractValidator<BookAppointmentRequest>
{
    public BookAppointmentRequestValidator()
    {
        RuleFor(x => x.PatientId).NotEmpty();
        RuleFor(x => x.TimeSlotId).NotEmpty();
        RuleFor(x => x.ReasonForVisit).NotEmpty().MaximumLength(200);
    }
}

