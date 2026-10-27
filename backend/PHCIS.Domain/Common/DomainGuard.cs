using System.Net.Mail;
using PHCIS.Domain.Exceptions;

namespace PHCIS.Domain.Common;

internal static class DomainGuard
{
    public static string RequiredText(string? value, int maximumLength, string field)
    {
        var normalized = OptionalText(value, maximumLength, field);
        return normalized ?? throw new DomainRuleException($"{field} is required.");
    }

    public static string? OptionalText(string? value, int maximumLength, string field)
    {
        var normalized = string.IsNullOrWhiteSpace(value) ? null : value.Trim();

        if (normalized?.Length > maximumLength)
        {
            throw new DomainRuleException($"{field} must not exceed {maximumLength} characters.");
        }

        return normalized;
    }

    public static string? Email(string? value)
    {
        var normalized = OptionalText(value, 254, "Email");

        if (normalized is not null &&
            (!MailAddress.TryCreate(normalized, out var address) || address.Address != normalized))
        {
            throw new DomainRuleException("Email must be a valid email address.");
        }

        return normalized;
    }
}

