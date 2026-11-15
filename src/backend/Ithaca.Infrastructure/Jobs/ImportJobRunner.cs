using Ithaca.Application.Imports;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Ithaca.Infrastructure.Jobs;

/// <summary>
/// Runs Letterboxd imports in the background (ADR-0008; never on a request path, ADR-0003). A job only schedules: each
/// round opens a DI scope and calls <see cref="ImportService.ProcessNextBatchAsync"/> once; after a batch it goes straight
/// on, when idle (or after a write conflict) it waits <see cref="ImportOptions.PollInterval"/>. A job left running by a
/// restart is simply picked up again. A round that throws (database down) is logged and retried after the pause instead
/// of stopping the host. Logs carry import ids and outcome codes only, never titles. Single instance: ADR-0016.
/// </summary>
internal sealed partial class ImportJobRunner(
    IServiceScopeFactory scopes, IOptions<ImportOptions> options, TimeProvider time, ILogger<ImportJobRunner> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.Value.JobEnabled)
        {
            LogDisabled(logger);
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            ImportBatchResult result;
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                result = await scope.ServiceProvider.GetRequiredService<ImportService>().ProcessNextBatchAsync(stoppingToken);
            }
            catch (Exception e) when (e is not OperationCanceledException || !stoppingToken.IsCancellationRequested)
            {
                LogRoundFailed(logger, e);
                result = new ImportBatchResult(ImportBatchOutcome.Retry, null);
            }

            if (result.Outcome == ImportBatchOutcome.Failed)
            {
                LogImportFailed(logger, result.ImportId);
            }
            else if (result.Outcome == ImportBatchOutcome.Completed)
            {
                LogImportCompleted(logger, result.ImportId);
            }

            if (result.Outcome is ImportBatchOutcome.Idle or ImportBatchOutcome.Retry)
            {
                await Task.Delay(options.Value.PollInterval, time, stoppingToken);
            }
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Import job runner is disabled (Imports:JobEnabled=false)")]
    private static partial void LogDisabled(ILogger logger);

    [LoggerMessage(Level = LogLevel.Information, Message = "Import {ImportId} completed")]
    private static partial void LogImportCompleted(ILogger logger, Guid? importId);

    [LoggerMessage(Level = LogLevel.Error, Message = "Import {ImportId} failed on an unexpected error; earlier batches are kept")]
    private static partial void LogImportFailed(ILogger logger, Guid? importId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Import round failed; retrying after the poll interval")]
    private static partial void LogRoundFailed(ILogger logger, Exception exception);
}
