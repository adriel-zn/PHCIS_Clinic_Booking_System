namespace PHCIS.Application.Models;

public sealed record ClinicDto(
    Guid Id,
    string Name,
    string Address,
    string? PhoneNumber);
