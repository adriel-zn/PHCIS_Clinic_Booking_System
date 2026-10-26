using System.Text.Json;
using System.Threading.Channels;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using PHCIS.Infrastructure.Persistence;
using Serilog.Core;
using Serilog.Events;

namespace PHCIS.Api.Observability;

/// <summary>Bounded, nonblocking Serilog sink; stores templates and allowlisted metadata, never raw payloads.</summary>
public sealed class DatabaseLogQueue : ILogEventSink
{
    internal static readonly AsyncLocal<bool> Writing = new();
    internal readonly Channel<LogEvent> Events = Channel.CreateBounded<LogEvent>(10000);
    /// <summary>Enqueues a log event while suppressing the sink's own database operations.</summary>
    public void Emit(LogEvent logEvent)
    {
        if (Writing.Value)
            return;
        if (!Events.Writer.TryWrite(logEvent))
            Console.Error.WriteLine("PHCIS log buffer is full; database logging is unavailable.");
    }
}

/// <summary>Separates normal activity from Error/Fatal events without re-logging its own database writes.</summary>
public sealed class DatabaseLogWriter(DatabaseLogQueue queue, IServiceScopeFactory scopes, IConfiguration configuration) : BackgroundService
{
    private static readonly HashSet<string> Allowed = ["ActorId", "TargetId", "AppointmentId", "ClinicId", "TraceId", "StatusCode", "RequestMethod", "Route", "Elapsed", "ExceptionType", "Entity", "Operation", "Count"];
    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!configuration.GetValue<bool>("DatabaseLogging:Enabled"))
            return;
        await foreach (var entry in queue.Events.Reader.ReadAllAsync(stoppingToken))
        {
            for (var attempt = 0; attempt < 3; attempt++)
            {
                try
                {
                    DatabaseLogQueue.Writing.Value = true;
                    await using var scope = scopes.CreateAsyncScope();
                    var context = scope.ServiceProvider.GetRequiredService<PhcisDbContext>();
                    var properties = JsonSerializer.Serialize(entry.Properties.Where(p => Allowed.Contains(p.Key)).ToDictionary(p => p.Key, p => p.Value.ToString()));
                    if (entry.Level >= LogEventLevel.Error)
                        context.ErrorLogs.Add(new ErrorLog { Timestamp = entry.Timestamp, Level = entry.Level.ToString(), Event = entry.MessageTemplate.Text, Properties = properties, ExceptionType = entry.Exception?.GetType().FullName });
                    else
                        context.ActivityLogs.Add(new ActivityLog { Timestamp = entry.Timestamp, Level = entry.Level.ToString(), Event = entry.MessageTemplate.Text, Properties = properties });
                    await context.SaveChangesAsync(stoppingToken);
                    break;
                }
                catch (Exception) when (!stoppingToken.IsCancellationRequested)
                {
                    if (attempt == 2)
                        Console.Error.WriteLine("PHCIS could not persist a log event after three attempts.");
                    else
                        await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken);
                }
                finally { DatabaseLogQueue.Writing.Value = false; }
            }
        }
    }
    /// <summary>Completes the queue and allows a bounded shutdown drain.</summary>
    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        queue.Events.Writer.TryComplete();
        if (ExecuteTask is not null)
            try
            { await ExecuteTask.WaitAsync(TimeSpan.FromSeconds(10), cancellationToken); }
            catch (TimeoutException) { }
        await base.StopAsync(cancellationToken);
    }
}

/// <summary>Audits entity operations without logging property values, passwords or clinical content.</summary>
public sealed class EntityAuditInterceptor(ILogger<EntityAuditInterceptor> logger) : SaveChangesInterceptor
{
    private List<(string Entity, string Operation)> _changes = [];
    /// <summary>Captures changed entity types and operations without property values.</summary>
    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        _changes = eventData.Context?.ChangeTracker.Entries().Where(e => e.Entity is not ActivityLog and not ErrorLog &&
            e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted)
            .Select(e => (e.Metadata.ClrType.Name, e.State.ToString())).Distinct().ToList() ?? [];
        return ValueTask.FromResult(result);
    }
    /// <summary>Logs metadata only after EF reports a successful save.</summary>
    public override ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData eventData, int result, CancellationToken cancellationToken = default)
    {
        foreach (var change in _changes)
            logger.LogInformation("Saved entity {Entity} operation {Operation}.", change.Entity, change.Operation);
        _changes.Clear();
        return ValueTask.FromResult(result);
    }
}
