using Microsoft.EntityFrameworkCore;
using PHCIS.Application.Abstractions;
using PHCIS.Domain.Entities;

namespace PHCIS.Infrastructure.Persistence.Repositories;

/// <summary>Uses the scoped context so booking does not insert existing patients again.</summary>
public sealed class PatientReader(PhcisDbContext context) : IPatientReader
{
    public Task<Patient?> FindAsync(Guid patientId, CancellationToken cancellationToken)
        => context.Patients.SingleOrDefaultAsync(x => x.Id == patientId, cancellationToken);
}

