using FluentValidation;
using FluentValidation.Results;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using PHCIS.Api.Contracts;
using PHCIS.Domain.Entities;
using PHCIS.Infrastructure.Identity;
using PHCIS.Infrastructure.Persistence;

namespace PHCIS.Api.Authentication;

/// <summary>Coordinates Identity and patient creation in one transaction; credentials never enter Domain.</summary>
public sealed class AccountService(
    PhcisDbContext context,
    UserManager<PortalUser> users,
    SignInManager<PortalUser> signIn,
    TimeProvider clock)
{
    /// <summary>Creates a linked Identity account and Domain patient without storing plain-text passwords.</summary>
    public async Task RegisterAsync(RegisterBody body, CancellationToken cancellationToken)
    {
        var patient = new Patient(body.FirstName, body.LastName, body.DateOfBirth, clock);
        patient.UpdateContactDetails(body.PhoneNumber, body.Email);
        patient.UpdateNationalId(body.NationalId);
        await context.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            await using var transaction = context.Database.IsRelational()
                ? await context.Database.BeginTransactionAsync(cancellationToken) : null;
            try
            {
                context.Patients.Add(patient);
                await context.SaveChangesAsync(cancellationToken);
                var user = new PortalUser
                {
                    UserName = body.Email.Trim(),
                    Email = body.Email.Trim(),
                    PhoneNumber = body.PhoneNumber.Trim(),
                    PatientId = patient.Id
                };
                var result = await users.CreateAsync(user, body.Password);
                if (!result.Succeeded)
                {
                    throw new ValidationException(result.Errors.Select(error =>
                        new ValidationFailure("Account", error.Description)));
                }
                var roleResult = await users.AddToRoleAsync(user, PortalRoles.Patient);
                if (!roleResult.Succeeded)
                    throw new InvalidOperationException("Patient role could not be assigned.");
                if (transaction is not null)
                {
                    await transaction.CommitAsync(cancellationToken);
                }
            }
            catch
            {
                if (transaction is not null)
                {
                    await transaction.RollbackAsync(cancellationToken);
                }
                else
                {
                    // Test/nonrelational stores have no transaction; avoid orphaning the new patient.
                    context.Patients.Remove(patient);
                    await context.SaveChangesAsync(cancellationToken);
                }
                context.ChangeTracker.Clear();
                throw;
            }
        });
    }

    /// <summary>Uses Identity password hashing, lockout, and persistent-cookie handling.</summary>
    public async Task<bool> LoginAsync(LoginBody body)
    {
        var user = await users.FindByEmailAsync(body.Email.Trim());
        if (user is not { IsActive: true, IsDeleted: false })
        {
            return false;
        }
        var result = await signIn.PasswordSignInAsync(user, body.Password, body.RememberMe, lockoutOnFailure: true);
        return result.Succeeded;
    }
}

