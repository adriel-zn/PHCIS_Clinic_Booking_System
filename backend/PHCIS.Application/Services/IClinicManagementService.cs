namespace PHCIS.Application.Services;

/// <summary>Creates clinics with an initial fortnight of weekday availability.</summary>
public interface IClinicManagementService
{
    Task<Guid> CreateAsync(CreateClinicRequest request, CancellationToken cancellationToken);
}

/// <summary>Clinic details supplied by an authorized clinic administrator.</summary>
public sealed record CreateClinicRequest(string Name, string Address, string? PhoneNumber);
