using System.Data;
using Microsoft.EntityFrameworkCore;
using PHCIS.Application.Abstractions;
using PHCIS.Domain.Entities;
using PHCIS.Domain.Exceptions;

namespace PHCIS.Infrastructure.Persistence;

/// <summary>Uses one transaction for clinic creation, duplicate checking, and initial availability.</summary>
public sealed class ClinicWriter(PhcisDbContext context) : IClinicWriter
{
    public async Task CreateAsync(Clinic clinic, IReadOnlyCollection<TimeSlot> slots, CancellationToken cancellationToken)
    {
        await context.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            // Serializable prevents concurrent creations from passing the same duplicate check.
            await using var transaction = context.Database.IsRelational()
                ? await context.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken) : null;
            if (await context.Clinics.AnyAsync(x => x.Name.ToUpper() == clinic.Name.ToUpper(), cancellationToken))
            {
                throw new DomainRuleException("A clinic with this name already exists.");
            }
            context.Clinics.Add(clinic);
            context.TimeSlots.AddRange(slots);
            await context.SaveChangesAsync(cancellationToken);
            if (transaction is not null)
                await transaction.CommitAsync(cancellationToken);
        });
    }
}
