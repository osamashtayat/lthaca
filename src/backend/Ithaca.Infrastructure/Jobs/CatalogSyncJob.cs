using Ithaca.Application.Catalog;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Ithaca.Infrastructure.Jobs;

/// <summary>
/// Schedules the nightly catalog sync. A job only schedules and opens a DI scope; the work is in
/// <see cref="CatalogSyncService"/> (same rule as controllers and hubs). Plain BackgroundService, no job framework (ADR-0008).
/// Single-instance assumption: see ADR-0016 before running more than one API replica.
/// </summary>
internal sealed partial class CatalogSyncJob(
    IServiceScopeFactory scopes, IOptions<CatalogSyncOptions> options, TimeProvider time, ILogger<CatalogSyncJob> logger) : BackgroundService
{
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
            await using var scope = scopes.CreateAsyncScope();
            var result = await scope.ServiceProvider.GetRequiredService<CatalogSyncService>().RunAsync(stoppingToken);
            if (result.IsSuccess)
            {
                LogFinished(logger, result.Value);
            }
            else
            {
                LogFailed(logger, result.Error.Code);
            }
        }
    }

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
