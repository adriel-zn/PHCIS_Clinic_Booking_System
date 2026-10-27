using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;

using PHCIS.Web.Configuration;
using PHCIS.Web.Models;
using PHCIS.Web.Services;

namespace PHCIS.Web.Components;

/// <summary>Owns availability selection state; persistence and authentication stay in the API client.</summary>
public partial class AvailabilityPanel : IDisposable
{
    [Inject] private IAppointmentApiClient Api { get; set; } = null!;
    [Inject] private PortalOptions Options { get; set; } = null!;
    [Inject] private TimeProvider Clock { get; set; } = null!;
    [Parameter] public EventCallback<AppointmentSummary> OnBooked { get; set; }
    [Parameter] public Guid? ClinicId { get; set; }
    [Parameter] public bool ReviewInSeparatePage { get; set; }
    [Parameter] public EventCallback<SlotSummary> OnSelected { get; set; }

    private readonly ClinicSearchModel _search = new();
    private readonly CancellationTokenSource _lifetime = new();
    private EditContext _editContext = null!;
    private AvailabilityQuery? _query;
    private AvailableSlotsPage? _page;
    private SlotSummary? _selected;
    private AppointmentApiException? _error;
    private bool _busy;
    private bool _disposed;

    protected override void OnInitialized()
    {
        _search.EarliestDate = DateOnly.FromDateTime(Clock.GetUtcNow().ToOffset(Options.ClinicOffset).DateTime);
        _search.Date = _search.EarliestDate;
        _search.ClinicReference = ClinicId?.ToString() ?? Options.DefaultClinicId ?? string.Empty;
        _editContext = new EditContext(_search);
        _editContext.OnFieldChanged += SearchChanged;
    }

    private void SearchChanged(object? sender, FieldChangedEventArgs args)
    {
        // Never let a selection from a previous clinic or day survive an edited search.
        _page = null;
        _selected = null;
        _query = null;
        _error = null;
    }

    private Task SearchAsync()
    {
        if (_busy)
        {
            return Task.CompletedTask;
        }
        _query = new AvailabilityQuery(Guid.Parse(_search.ClinicReference), _search.Date!.Value);
        return LoadAsync(0);
    }

    private async Task LoadAsync(int offset)
    {
        if (_busy || _query is null)
        {
            return;
        }
        _busy = true;
        _error = null;
        _page = null;
        _selected = null;
        _query = _query with { Offset = offset };

        try
        {
            _page = await Api.FindAvailableAsync(_query, _lifetime.Token);
        }
        catch (OperationCanceledException) when (_disposed) { }
        catch (Exception exception)
        {
            _error = SafeError(exception);
        }
        finally
        {
            _busy = false;
        }
    }

    private void Select(SlotSummary slot)
    {
        if (!_busy)
        {
            _selected = slot;
            _error = null;
        }
    }

    private Task PreviousAsync() => LoadAsync(Math.Max(0, _page!.Offset - _page.Limit));
    private Task NextAsync() => LoadAsync(_page!.Offset + _page.Limit);
    private string Time(DateTimeOffset instant) => instant.ToOffset(Options.ClinicOffset).ToString("HH:mm");

    private async Task BookAsync()
    {
        if (_busy || _selected is null)
        {
            return;
        }

        if (ReviewInSeparatePage)
        {
            await OnSelected.InvokeAsync(_selected);
            return;
        }

        _busy = true;
        _error = null;
        try
        {
            var result = await Api.BookAsync(_selected.Id, _lifetime.Token);
            _selected = null;
            _page = null;
            _query = null;
            if (!_disposed)
            {
                await OnBooked.InvokeAsync(result);
            }
        }
        catch (OperationCanceledException) when (_disposed) { }
        catch (Exception exception)
        {
            _error = SafeError(exception);
            if (_error.StatusCode == 409)
            {
                _selected = null;
                _page = null;
                _query = null;
            }
        }
        finally
        {
            _busy = false;
        }
    }

    private static AppointmentApiException SafeError(Exception exception) =>
        exception as AppointmentApiException
        ?? new AppointmentApiException(0, "Something went wrong. Please try again later.");

    public void Dispose()
    {
        _disposed = true;
        _editContext.OnFieldChanged -= SearchChanged;
        _lifetime.Cancel();
        _lifetime.Dispose();
    }
}

