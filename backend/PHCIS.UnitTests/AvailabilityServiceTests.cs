using FluentValidation;
using PHCIS.Application.Exceptions;
using PHCIS.Application.Models;

namespace PHCIS.UnitTests;

public sealed class AvailabilityServiceTests
{
    [Fact]
    public async Task AvailabilityReturnsBoundedMappedPageAndRequestsOneExtraRow()
    {
        using var fixture = new ApplicationServiceFixture();
        var slot = DomainTestData.Slot();
        fixture.Store.AvailabilityResults.AddRange([slot, DomainTestData.Slot()]);
        using var source = new CancellationTokenSource();
        var request = new AvailableSlotsRequest(
            slot.ClinicId,
            DomainTestData.Now.ToOffset(TimeSpan.FromHours(2)),
            DomainTestData.Now.AddDays(1),
            Offset: 3,
            Limit: 1);

        var result = await fixture.Availability().GetAvailableSlotsAsync(request, source.Token);

        Assert.Equal(slot.Id, Assert.Single(result.Items).Id);
        Assert.Equal(3, result.Offset);
        Assert.Equal(1, result.Limit);
        Assert.True(result.HasMore);
        Assert.Equal((DomainTestData.Now, request.To, DomainTestData.Now, 3, 2), fixture.Store.LastSearch);
        Assert.Equal(source.Token, fixture.Store.LastToken);
        Assert.Equal(0, fixture.Store.Saves);
    }

    [Fact]
    public async Task ExistingClinicWithNoAvailableSlotsReturnsEmptyPage()
    {
        using var fixture = new ApplicationServiceFixture();

        var result = await fixture.Availability().GetAvailableSlotsAsync(ValidRequest());

        Assert.Empty(result.Items);
        Assert.False(result.HasMore);
    }

    [Fact]
    public async Task UnknownClinicReturnsNotFound()
    {
        using var fixture = new ApplicationServiceFixture();
        fixture.Store.ClinicExists = false;

        await Assert.ThrowsAsync<ResourceNotFoundException>(() =>
            fixture.Availability().GetAvailableSlotsAsync(ValidRequest()));

        Assert.Equal(1, fixture.Store.Reads);
    }

    [Theory]
    [InlineData(-1, 50)]
    [InlineData(0, 0)]
    [InlineData(0, 101)]
    public async Task InvalidPagingIsRejectedBeforeReading(int offset, int limit)
    {
        using var fixture = new ApplicationServiceFixture();

        await Assert.ThrowsAsync<ValidationException>(() => fixture.Availability().GetAvailableSlotsAsync(
            ValidRequest() with { Offset = offset, Limit = limit }));

        Assert.Equal(0, fixture.Store.Reads);
    }

    [Fact]
    public async Task InvalidClinicOrIntervalIsRejectedBeforeReading()
    {
        using var fixture = new ApplicationServiceFixture();
        await Assert.ThrowsAsync<ValidationException>(() => fixture.Availability().GetAvailableSlotsAsync(
            ValidRequest() with { ClinicId = Guid.Empty }));
        await Assert.ThrowsAsync<ValidationException>(() => fixture.Availability().GetAvailableSlotsAsync(
            ValidRequest() with { To = DomainTestData.Now }));
        Assert.Equal(0, fixture.Store.Reads);
    }

    [Fact]
    public async Task CancelledTokenPreventsAvailabilityReads()
    {
        using var fixture = new ApplicationServiceFixture();
        using var source = new CancellationTokenSource();
        source.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            fixture.Availability().GetAvailableSlotsAsync(ValidRequest(), source.Token));

        Assert.Equal(0, fixture.Store.Reads);
    }

    private static AvailableSlotsRequest ValidRequest() =>
        new(Guid.NewGuid(), DomainTestData.Now, DomainTestData.Now.AddDays(1));
}

