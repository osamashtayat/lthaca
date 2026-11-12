using Ithaca.Application.Ai;
using Ithaca.Application.Common;
using Ithaca.Application.Tmdb;
using Microsoft.Extensions.Logging;

namespace Ithaca.Application.Catalog;

/// <summary>
/// Nightly TMDB → <c>films</c> sync with embeddings (ADR-0003). Called by <c>CatalogSyncJob</c> (Infrastructure), which only
/// schedules. STUB: the sync itself is a feature task; this only fixes where the logic goes and what it depends on.
/// </summary>
public sealed partial class CatalogSyncService(
    IAppDbContext db, ITmdbClient tmdb, IEmbeddingClient embeddings, TimeProvider time, ILogger<CatalogSyncService> logger)
{
    public Task<Result> RunAsync(CancellationToken ct)
    {
        // TODO(catalog-sync): page through tmdb.GetCatalogPageAsync, upsert films by TmdbId, embed new/changed
        // overviews with embeddings.EmbedAsync, then db.SaveChangesAsync once per batch.
        _ = (db, tmdb, embeddings, time);
        LogNotImplemented(logger);
        return Task.FromResult(Result.Success());
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Catalog sync is not implemented yet; nothing synced")]
    private static partial void LogNotImplemented(ILogger logger);
}
