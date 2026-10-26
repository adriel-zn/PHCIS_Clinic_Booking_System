using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using PHCIS.Api.Development;
using PHCIS.Domain.Entities;
using PHCIS.Infrastructure.Persistence;

namespace PHCIS.UnitTests;

public sealed class ClinicDevelopmentDataTests
{
    [Fact]
    public async Task RepeatedSeedingPreservesAppointmentsAndAddsNoDuplicateSlots()
    {
        using var context = new PhcisDbContext(new DbContextOptionsBuilder<PhcisDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var clock = new TestClock(DomainTestData.Now);
        await ClinicDevelopmentData.SeedAsync(context, clock);
        var slots = await context.TimeSlots.ToListAsync();
        var booking = new Appointment(DomainTestData.Patient(), slots[0], clock);
        context.Appointments.Add(booking);
        await context.SaveChangesAsync();

        await ClinicDevelopmentData.SeedAsync(context, clock);

        Assert.Equal(4, await context.Clinics.CountAsync());
        Assert.Equal(slots.Count, await context.TimeSlots.CountAsync());
        Assert.Equal(booking.Id, (await context.Appointments.SingleAsync()).Id);
        Assert.Empty(await context.Users.ToListAsync());
        Assert.All(slots, slot =>
        {
            var localStart = slot.StartsAt.ToOffset(TimeSpan.FromHours(2));
            Assert.True(slot.StartsAt > clock.GetUtcNow());
            Assert.DoesNotContain(localStart.DayOfWeek, new[] { DayOfWeek.Saturday, DayOfWeek.Sunday });
            Assert.InRange(localStart.Hour, 8, 16);
            Assert.Equal(TimeSpan.FromMinutes(30), slot.EndsAt - slot.StartsAt);
        });
        Assert.Equal(slots.Count, slots.Select(x => (x.ClinicId, x.StartsAt)).Distinct().Count());
    }

    [Fact]
    public async Task AdvancingDateRefreshesFutureAvailabilityWithoutRemovingOldSlots()
    {
        using var context = new PhcisDbContext(new DbContextOptionsBuilder<PhcisDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        await ClinicDevelopmentData.SeedAsync(context, new TestClock(DomainTestData.Now));
        var originalIds = await context.TimeSlots.Select(x => x.Id).ToListAsync();
        await ClinicDevelopmentData.SeedAsync(context, new TestClock(DomainTestData.Now.AddDays(7)));

        Assert.True(await context.TimeSlots.CountAsync() > originalIds.Count);
        Assert.Equal(originalIds.Count, await context.TimeSlots.CountAsync(x => originalIds.Contains(x.Id)));
    }

    [Theory]
    [InlineData("Production", "true")]
    [InlineData("Development", "false")]
    public async Task DisabledOrNonDevelopmentSeedingDoesNotResolveDatabase(string environment, string enabled)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = environment });
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Development:SeedClinicsAndSlots"] = enabled
        });
        await using var app = builder.Build();
        await ClinicDevelopmentData.InitializeAsync(app);
    }
}
