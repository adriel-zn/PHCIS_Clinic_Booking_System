using System.ComponentModel.DataAnnotations;
using PHCIS.Web.Validation;

namespace PHCIS.Web.Models;

public sealed class CancellationModel : IValidatableObject
{
    [NonEmptyGuid(ErrorMessage = "Enter a valid appointment reference.")]
    public string AppointmentReference { get; set; } = string.Empty;

    [Required(ErrorMessage = "Tell us why you need to cancel.")]
    public string Reason { get; set; } = string.Empty;

    public bool Confirmed { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (!Confirmed)
        {
            yield return new ValidationResult("Confirm that you want to cancel this appointment.", [nameof(Confirmed)]);
        }
        if (Reason?.Trim().Length > 500)
        {
            yield return new ValidationResult("Keep the reason to 500 characters or fewer.", [nameof(Reason)]);
        }
    }
}

