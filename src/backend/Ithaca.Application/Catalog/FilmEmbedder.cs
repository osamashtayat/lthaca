using Ithaca.Application.Ai;
using Ithaca.Application.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Ithaca.Application.Catalog;

/// <summary>
/// Embeds each film's Wikidata text (<c>FilmFacts.EmbeddingText</c>) into <c>films.embedding</c> (ADR-0013, ADR-0017). A
/// film is (re-)embedded when it has facts and no vector yet, its vector came from another model, or its facts' text
/// changed since (<c>Film.EmbeddingSourceHash</c> ≠ <c>FilmFacts.EmbeddingTextHash</c>). Films without facts are never
/// embedded: no TMDB text reaches the model. Works in TMDB-id order, <see cref="CatalogSyncOptions.EmbeddingBatchSize"/>
/// films per request and one save per batch, so a failure keeps finished batches and the next run continues.
/// A collaborator of <see cref="CatalogSyncService"/>, its only caller, not a use-case service of its own.
/// </summary>
public sealed class FilmEmbedder(IAppDbContext db, IEmbeddingClient embeddings, IOptions<CatalogSyncOptions> options)
{
    /// <summary>Returns how many films got a new vector.</summary>
    public async Task<Result<int>> RunAsync(CancellationToken ct)
    {
        var model = embeddings.ModelName;
        var batchSize = options.Value.EmbeddingBatchSize;
        int embedded = 0, lastTmdbId = 0;

        while (true)
        {
            var afterTmdbId = lastTmdbId;
            var batch = await db.Films
                .Join(db.FilmFacts, f => f.Id, x => x.FilmId, (f, x) => new { Film = f, x.EmbeddingText, x.EmbeddingTextHash })
                .Where(r => r.Film.TmdbId > afterTmdbId
                    && (r.Film.Embedding == null || r.Film.EmbeddingModel != model || r.Film.EmbeddingSourceHash != r.EmbeddingTextHash))
                .OrderBy(r => r.Film.TmdbId)
                .Take(batchSize)
                .ToListAsync(ct);
            if (batch.Count == 0)
            {
                break;
            }

            var vectors = await embeddings.EmbedAsync([.. batch.Select(r => r.EmbeddingText)], EmbeddingInputType.Document, ct);
            if (!vectors.IsSuccess)
            {
                return vectors.Error;
            }

            if (vectors.Value.Count != batch.Count)
            {
                return AiErrors.InvalidEmbedding($"{vectors.Value.Count} vectors for {batch.Count} texts");
            }

            for (var i = 0; i < batch.Count; i++)
            {
                batch[i].Film.SetEmbedding(vectors.Value[i], model, batch[i].EmbeddingTextHash);
            }

            await db.SaveChangesAsync(ct);
            embedded += batch.Count;
            lastTmdbId = batch[^1].Film.TmdbId;
        }

        return embedded;
    }
}
