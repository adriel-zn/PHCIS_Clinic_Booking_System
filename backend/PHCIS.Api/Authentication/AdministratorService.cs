using Microsoft.AspNetCore.Identity;
using PHCIS.Application.Exceptions;
using PHCIS.Domain.Exceptions;
using PHCIS.Infrastructure.Identity;
using PHCIS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using System.Data;

namespace PHCIS.Api.Authentication;

/// <summary>Manages administrator membership through standard ASP.NET Identity role tables.</summary>
public sealed class AdministratorService(UserManager<PortalUser> users, RoleManager<IdentityRole> roles,
    PhcisDbContext context, ILogger<AdministratorService> logger)
{
    // The SQL transaction protects multiple API instances; this gate also supports nonrelational test stores.
    private static readonly SemaphoreSlim RoleChanges = new(1, 1);

    /// <summary>Lists current administrators using AspNetUsers and AspNetUserRoles.</summary>
    public async Task<IReadOnlyList<AdministratorDetails>> ListAsync(string actorId)
    {
        var administrators = await users.GetUsersInRoleAsync(PortalRoles.Admin);
        return administrators.OrderBy(x => x.Email).Select(x => new AdministratorDetails(
            x.Id, x.Email ?? x.UserName ?? "", x.Id == actorId, administrators.Count > 1)).ToArray();
    }

    /// <summary>Removes Admin membership while retaining the account and Patient role.</summary>
    public Task RemoveAsync(string accountId, string actorId) => ChangeAsync(async () =>
    {
        var user = await users.FindByIdAsync(accountId)
            ?? throw new ResourceNotFoundException("The account was not found.");
        if (!await users.IsInRoleAsync(user, PortalRoles.Admin))
            return;
        var role = await roles.FindByNameAsync(PortalRoles.Admin)
            ?? throw new InvalidOperationException("Admin role is missing.");
        if (await context.UserRoles.CountAsync(x => x.RoleId == role.Id) <= 1)
            throw new DomainRuleException("The last administrator cannot be removed. Add another administrator first.");
        EnsureSuccess(await users.RemoveFromRoleAsync(user, PortalRoles.Admin));
        logger.LogInformation("Administrator {ActorId} removed administrator access from account {TargetId}.", actorId, user.Id);
    });

    /// <summary>Grants clinic administration idempotently without changing credentials.</summary>
    public Task GrantAsync(string email, string actorId) => ChangeAsync(async () =>
    {
        var user = await users.FindByEmailAsync(email.Trim())
            ?? throw new ResourceNotFoundException("No registered account has this email. Ask the person to register first.");
        if (await users.IsInRoleAsync(user, PortalRoles.Admin))
            return;
        EnsureSuccess(await users.AddToRoleAsync(user, PortalRoles.Admin));
        logger.LogInformation("Administrator {ActorId} granted clinic administration to account {TargetId}.", actorId, user.Id);
    });

    private static void EnsureSuccess(IdentityResult result)
    {
        if (!result.Succeeded)
            throw new DomainRuleException("Administrator access could not be saved. Refresh and try again.");
    }

    private async Task ChangeAsync(Func<Task> change)
    {
        await RoleChanges.WaitAsync();
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
        finally { RoleChanges.Release(); }
    }
}

/// <summary>Administrator directory entry without passwords or patient information.</summary>
public sealed record AdministratorDetails(string Id, string Email, bool IsCurrentUser, bool CanRemove);
