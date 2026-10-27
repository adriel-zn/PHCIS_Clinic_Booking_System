using System.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using PHCIS.Application.Exceptions;
using PHCIS.Domain.Exceptions;
using PHCIS.Infrastructure.Identity;
using PHCIS.Infrastructure.Persistence;

namespace PHCIS.Api.Authentication;

/// <summary>Manages only Staff membership in the standard Identity role tables.</summary>
public sealed class StaffService(UserManager<PortalUser> users, PhcisDbContext context, ILogger<StaffService> logger)
{
    private static readonly SemaphoreSlim StaffChanges = new(1, 1);

    /// <summary>Lists Staff members without returning passwords or patient information.</summary>
    public async Task<IReadOnlyList<StaffDetails>> ListAsync(string actorId) =>
        (await users.GetUsersInRoleAsync(PortalRoles.Staff)).Where(x => x.IsActive && !x.IsDeleted).OrderBy(x => x.Email)
            .Select(x => new StaffDetails(x.Id, x.Email ?? x.UserName ?? "", x.Id == actorId)).ToArray();

    /// <summary>Assigns Staff to an existing account without changing other memberships.</summary>
    public Task AssignAsync(string email, string actorId) => ChangeAsync(async () =>
    {
        var user = await users.FindByEmailAsync(email.Trim())
            ?? throw new ResourceNotFoundException("No registered account has this email. Ask the person to register first.");
        if (!user.IsActive || user.IsDeleted)
            throw new DomainRuleException("Activate this account before assigning Staff.");
        if (await users.IsInRoleAsync(user, PortalRoles.Staff))
            return;
        EnsureSuccess(await users.AddToRoleAsync(user, PortalRoles.Staff));
        logger.LogInformation("Administrator {ActorId} assigned Staff to account {TargetId}.", actorId, user.Id);
    });

    /// <summary>Removes only Staff; the account, credentials, Admin and Patient roles remain.</summary>
    public Task RemoveAsync(string accountId, string actorId) => ChangeAsync(async () =>
    {
        var user = await users.FindByIdAsync(accountId)
            ?? throw new ResourceNotFoundException("The account was not found.");
        if (!await users.IsInRoleAsync(user, PortalRoles.Staff))
            return;
        EnsureSuccess(await users.RemoveFromRoleAsync(user, PortalRoles.Staff));
        logger.LogInformation("Administrator {ActorId} removed Staff from account {TargetId}.", actorId, user.Id);
    });

    private static void EnsureSuccess(IdentityResult result)
    {
        if (!result.Succeeded)
            throw new DomainRuleException("Staff membership could not be saved. Refresh and try again.");
    }

    private async Task ChangeAsync(Func<Task> change)
    {
        await StaffChanges.WaitAsync();
        try
        {
            await context.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
            {
                await using var transaction = context.Database.IsRelational()
                    ? await context.Database.BeginTransactionAsync(IsolationLevel.Serializable) : null;
                await change();
                if (transaction is not null)
                    await transaction.CommitAsync();
            });
        }
        finally { StaffChanges.Release(); }
    }
}

/// <summary>A Staff member's directory identity, excluding credentials and patient data.</summary>
public sealed record StaffDetails(string Id, string Email, bool IsCurrentUser);
