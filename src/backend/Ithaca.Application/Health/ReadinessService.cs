using Ithaca.Application.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Ithaca.Application.Health;

/// <summary>
/// Readiness (ADR-0028): the API can serve because its database answers. Used by the Docker healthcheck and the uptime
/// monitor. Liveness (<c>GET /api/health</c>) needs no service: it only proves the process answers.
/// </summary>
public sealed partial class ReadinessService(IAppDbContext db, ILogger<ReadinessService> logger)
{
    /// <summary>Longest the check waits for the database before calling it unavailable.</summary>
    internal static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);

    public async Task<Result<HealthDto>> CheckAsync(CancellationToken ct)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(Timeout);
        try
        {
            // Any read of a migrated table: proves the connection, credentials and schema at once.
            await db.Films.AsNoTracking().AnyAsync(timeout.Token);
            return HealthDto.Ok;
        }
        catch (Exception e) when (!ct.IsCancellationRequested)
        {
            LogUnavailable(logger, e);
            return HealthErrors.DatabaseUnavailable;
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Readiness check failed: the database can't be reached")]
    private static partial void LogUnavailable(ILogger logger, Exception exception);
}
