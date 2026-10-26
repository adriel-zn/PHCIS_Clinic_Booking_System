using System.ComponentModel.DataAnnotations;
using PHCIS.Web.Validation;

namespace PHCIS.Web.Models;

public sealed class ClinicSearchModel : IValidatableObject
{
    [NonEmptyGuid(ErrorMessage = "Enter the clinic reference supplied by your clinic.")]
    public string ClinicReference { get; set; } = string.Empty;

    [Required(ErrorMessage = "Choose a date for your visit.")]
    public DateOnly? Date { get; set; }

    // Set from the injected clock using clinic time, not the browser's local calendar.
    public DateOnly EarliestDate { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (Date is not null && (Date < EarliestDate || Date == DateOnly.MaxValue))
        {
            yield return new ValidationResult("Choose today or a future date.", [nameof(Date)]);
        }
    }
}

