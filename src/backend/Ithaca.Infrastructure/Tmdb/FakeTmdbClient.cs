using Ithaca.Application.Tmdb;

namespace Ithaca.Infrastructure.Tmdb;

/// <summary>Offline TMDB stand-in (<c>Tmdb:Client = Fake</c>). Deterministic, no network. Used in dev, tests and evals.</summary>
internal sealed class FakeTmdbClient : ITmdbClient
{
    private static readonly TmdbFilmDetails[] Films =
    [
        new(550, "Fight Club", 1999, "An insomniac office worker crosses paths with a soap maker.", null, 139, ["Drama"]),
        new(27205, "Inception", 2010, "A thief who steals secrets through dream-sharing technology.", null, 148, ["Action", "Science Fiction"]),
        new(496243, "Parasite", 2019, "A poor family schemes to become employed by a wealthy household.", null, 133, ["Comedy", "Thriller", "Drama"]),
    ];

    public Task<IReadOnlyList<TmdbFilmSummary>> GetCatalogPageAsync(int page, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<TmdbFilmSummary>>(page == 1
            ? [.. Films.Select((f, i) => new TmdbFilmSummary(f.TmdbId, f.Title, f.ReleaseYear, Popularity: 100 - i))]
            : []);

    public Task<TmdbFilmDetails?> GetFilmDetailsAsync(int tmdbId, CancellationToken ct) =>
        Task.FromResult(Films.FirstOrDefault(f => f.TmdbId == tmdbId));

    public Task<IReadOnlyList<TmdbVideo>> GetTrailersAsync(int tmdbId, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<TmdbVideo>>([]);
}
