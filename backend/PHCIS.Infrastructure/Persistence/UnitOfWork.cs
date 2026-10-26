using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using PHCIS.Application.Abstractions;
using PHCIS.Application.Exceptions;

namespace PHCIS.Infrastructure.Persistence;

/// <summary>Commits one scoped use case and translates expected database write conflicts.</summary>
public sealed class UnitOfWork(PhcisDbContext context) : IUnitOfWork
{
    public async Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException exception)
        {
            DetachFailedEntries(exception);
            throw new AppointmentConflictException("The appointment changed. Reload it before trying again.");
        }
        catch (DbUpdateException exception)
            when (exception.InnerException is SqlException { Number: 2601 or 2627 })
        {
            DetachFailedEntries(exception);
            throw new AppointmentConflictException("The booking conflicts with an existing appointment.");
        }
    }

    private static void DetachFailedEntries(DbUpdateException exception)
    {
        // Failed changes must not be inadvertently retried by a later save in the same scope.
        foreach (var entry in exception.Entries)
        {
            entry.State = EntityState.Detached;
        }
    }
}

