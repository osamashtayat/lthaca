using Ithaca.Application.Catalog;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Sentry;

namespace Ithaca.Infrastructure.Jobs;

/// <summary>
/// Schedules the nightly catalog sync. A job only schedules and opens a DI scope; the work is in
/// <see cref="CatalogSyncService"/> (same rule as controllers and hubs). Plain BackgroundService, no job framework (ADR-0008).
/// Single-instance assumption: see ADR-0016 before running more than one API replica.
/// <para>Each run sends Sentry cron check-ins (<see cref="MonitorSlug"/>, ADR-0028): in progress, then ok or error, so a sync
/// that fails, hangs or never starts raises an alert. A no-op when Sentry is off.</para>
/// </summary>
internal sealed partial class CatalogSyncJob(
    IServiceScopeFactory scopes, IOptions<CatalogSyncOptions> options, TimeProvider time, ILogger<CatalogSyncJob> logger) : BackgroundService
{
    internal const string MonitorSlug = "catalog-sync";

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.Value.Enabled)
        {
            LogDisabled(logger);
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(DelayUntilNextRun(time.GetUtcNow(), options.Value.RunAtUtc), time, stoppingToken);
            var checkIn = SentrySdk.CaptureCheckIn(MonitorSlug, CheckInStatus.InProgress, configureMonitorOptions: monitor => Describe(monitor, options.Value.RunAtUtc));
            var succeeded = false;
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                var result = await scope.ServiceProvider.GetRequiredService<CatalogSyncService>().RunAsync(stoppingToken);
                succeeded = result.IsSuccess;
                if (result.IsSuccess)
                {
                    LogFinished(logger, result.Value);
                }
                else
                {
                    LogFailed(logger, result.Error.Code);
                }
            }
            finally
            {
                // A failure or exception reports an error check-in (the exception itself is logged by the host). A shutdown
                // mid-run (a deploy) sends none, so a restart doesn't page anyone; the next run, or MaxRuntime, settles it.
                if (!stoppingToken.IsCancellationRequested)
                {
                    SentrySdk.CaptureCheckIn(MonitorSlug, succeeded ? CheckInStatus.Ok : CheckInStatus.Error, checkIn);
                }
            }
        }
    }

    /// <summary>The monitor Sentry creates or updates from the first check-in: daily at the configured UTC time.</summary>
    internal static void Describe(SentryMonitorOptions monitor, TimeOnly runAtUtc)
    {
        monitor.Interval(Crontab(runAtUtc));
        monitor.TimeZone = "Etc/UTC";
        monitor.CheckInMargin = TimeSpan.FromMinutes(30);
        monitor.MaxRuntime = TimeSpan.FromHours(3);
    }

    /// <summary>A daily crontab for <paramref name="runAtUtc"/>, e.g. 03:00 → <c>0 3 * * *</c>.</summary>
    internal static string Crontab(TimeOnly runAtUtc) =>
        string.Create(System.Globalization.CultureInfo.InvariantCulture, $"{runAtUtc.Minute} {runAtUtc.Hour} * * *");

    internal static TimeSpan DelayUntilNextRun(DateTimeOffset nowUtc, TimeOnly runAtUtc)
    {
        var next = new DateTimeOffset(DateOnly.FromDateTime(nowUtc.UtcDateTime), runAtUtc, TimeSpan.Zero);
        return (next > nowUtc ? next : next.AddDays(1)) - nowUtc;
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Catalog sync job is disabled (CatalogSync:Enabled=false)")]
    private static partial void LogDisabled(ILogger logger);

    [LoggerMessage(Level = LogLevel.Information, Message = "Catalog sync finished: {Report}")]
    private static partial void LogFinished(ILogger logger, CatalogSyncReport report);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Catalog sync stopped with {Code}; pages already saved are kept")]
    private static partial void LogFailed(ILogger logger, string code);
}
