using System.ComponentModel.DataAnnotations;

namespace PHCIS.Web.Validation;

/// <summary>Validates text references without exposing backend types to the UI.</summary>
public sealed class NonEmptyGuidAttribute : ValidationAttribute
{
    public override bool IsValid(object? value) =>
        value is string text && Guid.TryParse(text, out var id) && id != Guid.Empty;
}

