using Ithaca.Application.Common;
using Ithaca.Application.Tmdb;

namespace Ithaca.Infrastructure.Tmdb;

/// <summary>Offline TMDB stand-in (<c>Tmdb:Client = Fake</c>). Deterministic, no network. Used in dev, tests and evals.</summary>
internal sealed class FakeTmdbClient : ITmdbClient
{
    /// <summary>The sample films; also what <c>DevDataSeeder</c> puts into an empty development database.</summary>
    internal static readonly TmdbFilmDetails[] Films =
    [
        new(550, "Fight Club", 1999, "An insomniac office worker crosses paths with a soap maker.", null, 139, ["Drama"], 100, "/fight-club-backdrop.jpg", 8.4, 29000, "en"),
        new(27205, "Inception", 2010, "A thief who steals secrets through dream-sharing technology.", null, 148, ["Action", "Science Fiction"], 99, "/inception-backdrop.jpg", 8.4, 37000, "en"),
        new(496243, "Parasite", 2019, "A poor family schemes to become employed by a wealthy household.", null, 133, ["Comedy", "Thriller", "Drama"], 98, "/parasite-backdrop.jpg", 8.5, 19000, "ko"),
    ];

    public Task<Result<IReadOnlyList<TmdbFilmSummary>>> GetCatalogPageAsync(int page, CancellationToken ct) =>
        Task.FromResult<Result<IReadOnlyList<TmdbFilmSummary>>>(page == 1
            ? Films.Select(f => new TmdbFilmSummary(f.TmdbId, f.Title, f.ReleaseYear, f.Popularity ?? 0)).ToList()
            : []);

    public Task<Result<TmdbFilmDetails?>> GetFilmDetailsAsync(int tmdbId, CancellationToken ct) =>
        Task.FromResult(Result<TmdbFilmDetails?>.Success(Films.FirstOrDefault(f => f.TmdbId == tmdbId)));

    public Task<Result<IReadOnlyList<TmdbVideo>>> GetTrailersAsync(int tmdbId, CancellationToken ct) =>
        Task.FromResult<Result<IReadOnlyList<TmdbVideo>>>(Array.Empty<TmdbVideo>());
}
