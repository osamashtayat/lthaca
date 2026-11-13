using Ithaca.Application.Common;
using Ithaca.Application.Wikidata;
using Ithaca.Domain.Films;

namespace Ithaca.Infrastructure.Wikidata;

/// <summary>
/// Offline Wikidata stand-in (<c>Wikidata:Client = Fake</c>). Deterministic, no network. Knows two of the three Fake TMDB
/// films (550, 27205; trimmed from a real reply) and deliberately not 496243, so dev and tests see an unmatched film.
/// </summary>
internal sealed class FakeWikidataClient : IWikidataClient
{
    internal static readonly WikidataFilm[] Films =
    [
        new(550, new WikidataFacts(
            "Q190050",
            ["drama film", "psychological thriller", "thriller film"],
            ["alter ego"],
            ["David Fincher"],
            ["Brad Pitt", "Edward Norton", "Jared Leto", "Helena Bonham Carter", "Meat Loaf"],
            ["United States", "Germany"],
            ["English"],
            1999,
            139,
            ["Empire Awards, UK for best british actress"])),
        new(27205, new WikidataFacts(
            "Q25188",
            ["heist film", "science fiction film", "thriller film"],
            ["dream world"],
            [],
            ["Leonardo DiCaprio", "Tom Hardy", "Marion Cotillard", "Elliot Page"],
            ["United Kingdom", "United States"],
            ["English", "Japanese", "French"],
            2010,
            148,
            ["Academy Award for Best Cinematography", "Academy Award for Best Visual Effects"])),
    ];

    public Task<Result<IReadOnlyList<WikidataFilm>>> GetFilmsAsync(IReadOnlyCollection<int> tmdbIds, CancellationToken ct) =>
        Task.FromResult<Result<IReadOnlyList<WikidataFilm>>>(Films.Where(f => tmdbIds.Contains(f.TmdbId)).ToList());
}
