using HotChocolate;
using Microsoft.EntityFrameworkCore;
using PHCIS.Api.Authentication;
using PHCIS.Application.Models;
using PHCIS.Application.Services;
using PHCIS.Infrastructure.Persistence;
namespace PHCIS.Api.GraphQL;

/// <summary>Bounded read-only projections with fresh authorization and separate EF scopes per resolver.</summary>
public sealed class PortalQuery
{
    /// <summary>Returns a bounded projection of the current patient's own appointments.</summary>
    public async Task<IReadOnlyList<AppointmentDetails>> GetMyAppointmentsAsync(int offset, int limit,
        [Service] IHttpContextAccessor http, [Service] IServiceScopeFactory scopes, CancellationToken ct)
    {
        if (offset < 0 || limit is < 1 or > 100)
            throw new GraphQLException("Invalid pagination.");
        await using var scope = scopes.CreateAsyncScope();
        var user = await scope.ServiceProvider.GetRequiredService<AccountAccess>().UserAsync(http.HttpContext!.User);
        if (user is null)
            throw new GraphQLException("Access denied.");
        var context = scope.ServiceProvider.GetRequiredService<PhcisDbContext>();
        return await context.Appointments.AsNoTracking().Where(a => a.PatientId == user.PatientId)
            .OrderByDescending(a => a.TimeSlot.StartsAt).ThenBy(a => a.Id).Skip(offset).Take(limit)
            .Select(a => new AppointmentDetails(a.Id, a.TimeSlotId, a.TimeSlot.ClinicId, a.TimeSlot.Clinic.Name,
                a.TimeSlot.Clinic.Address, a.TimeSlot.StartsAt, a.TimeSlot.EndsAt, a.Status.ToString(), a.ReasonForVisit)).ToListAsync(ct);
    }
    /// <summary>Returns clinic appointments only after checking current staff assignment.</summary>
    public async Task<StaffAppointmentsPage> GetClinicAppointmentsAsync(Guid clinicId, DateOnly date, string? status, int offset, int limit,
        [Service] IHttpContextAccessor http, [Service] IServiceScopeFactory scopes, CancellationToken ct)
    {
        await using var scope = scopes.CreateAsyncScope();
        if (!await scope.ServiceProvider.GetRequiredService<AccountAccess>().ClinicAsync(http.HttpContext!.User, clinicId))
            throw new GraphQLException("Access denied.");
        try
        { return await scope.ServiceProvider.GetRequiredService<IStaffAppointmentService>().ListAsync(new StaffAppointmentsRequest(clinicId, date, status, offset, limit), ct); }
        catch (FluentValidation.ValidationException) { throw new GraphQLException("Invalid clinic appointment filters."); }
    }
}
