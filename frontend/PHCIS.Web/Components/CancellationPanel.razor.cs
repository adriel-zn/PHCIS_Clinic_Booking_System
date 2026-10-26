using Microsoft.AspNetCore.Components;

using PHCIS.Web.Models;
using PHCIS.Web.Services;

namespace PHCIS.Web.Components;

/// <summary>Cancels by saved reference; the API independently enforces patient ownership and timing.</summary>
public partial class CancellationPanel : IDisposable
{
    [Inject] private IAppointmentApiClient Api { get; set; } = null!;
    [Parameter] public Guid? AppointmentReference { get; set; }
    [Parameter] public EventCallback<AppointmentSummary> OnCancelled { get; set; }

    private readonly CancellationModel _model = new();
    private readonly CancellationTokenSource _lifetime = new();
    private Guid? _lastProvidedReference;
    private AppointmentApiException? _error;
    private bool _busy;
    private bool _disposed;

    protected override void OnParametersSet()
    {
        if (AppointmentReference is not null && AppointmentReference != _lastProvidedReference)
        {
            _model.AppointmentReference = AppointmentReference.Value.ToString();
            _lastProvidedReference = AppointmentReference;
            _model.Reason = string.Empty;
            _model.Confirmed = false;
            _error = null;
        }
    }

    private async Task CancelAsync()
    {
        if (_busy)
        {
            return;
        }
        _busy = true;
        _error = null;
        try
        {
            var result = await Api.CancelAsync(
                Guid.Parse(_model.AppointmentReference), _model.Reason, _lifetime.Token);
            _model.Reason = string.Empty;
            _model.Confirmed = false;
            if (!_disposed)
            {
                await OnCancelled.InvokeAsync(result);
            }
        }
        catch (OperationCanceledException) when (_disposed) { }
        catch (Exception exception)
        {
            _error = exception as AppointmentApiException
                ?? new AppointmentApiException(0, "Something went wrong. Please try again later.");
        }
        finally
        {
            _busy = false;
        }
    }

    public void Dispose()
    {
        _disposed = true;
        _lifetime.Cancel();
        _lifetime.Dispose();
    }
}

