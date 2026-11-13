using Ithaca.Application.Common;
using Ithaca.Domain.Films;

namespace Ithaca.Application.Wikidata;

/// <summary>
/// Wikidata (CC0) access for the catalog sync: the AI-safe film facts (ADR-0017). Films are linked by Wikidata's
/// "TMDB movie ID" property (P4947), never by title. Failures (endpoint down, rate limited, timeout) are
/// <see cref="WikidataErrors.Unavailable"/>.
/// </summary>
public interface IWikidataClient
{
    /// <summary>Facts for the films Wikidata links to these TMDB ids. Ids it doesn't know are simply absent from the result.</summary>
    Task<Result<IReadOnlyList<WikidataFilm>>> GetFilmsAsync(IReadOnlyCollection<int> tmdbIds, CancellationToken ct);
}

public sealed record WikidataFilm(int TmdbId, WikidataFacts Facts);
