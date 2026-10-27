using FluentValidation;
using PHCIS.Application.Models;

namespace PHCIS.Application.Validation;

public sealed class RescheduleAppointmentRequestValidator : AbstractValidator<RescheduleAppointmentRequest>
{
    public RescheduleAppointmentRequestValidator()
    {
        RuleFor(x => x.AppointmentId).NotEmpty();
        RuleFor(x => x.PatientId).NotEmpty();
        RuleFor(x => x.TimeSlotId).NotEmpty();
    }
}

