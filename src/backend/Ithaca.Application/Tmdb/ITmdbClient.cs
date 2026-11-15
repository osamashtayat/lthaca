using Ithaca.Application.Common;

namespace Ithaca.Application.Tmdb;

/// <summary>
/// TMDB access. Callers: the nightly catalog sync, the Letterboxd import job (search and details for films we lack, in the
/// background) and cached details such as trailers. Never used to build decks:
/// candidates come from our own <c>films</c> table (ADR-0003). Failures (TMDB down, rate limited, bad key) are
/// <see cref="TmdbErrors.Unavailable"/>; an unknown film is a successful <c>null</c>.
/// </summary>
public interface ITmdbClient
{
    /// <summary>One page (20 films) of the catalog, most popular first. Empty when past the last page.</summary>
    Task<Result<IReadOnlyList<TmdbFilmSummary>>> GetCatalogPageAsync(int page, CancellationToken ct);

    /// <summary>Full details for one film, or null when TMDB doesn't know it.</summary>
    Task<Result<TmdbFilmDetails?>> GetFilmDetailsAsync(int tmdbId, CancellationToken ct);

    Task<Result<IReadOnlyList<TmdbVideo>>> GetTrailersAsync(int tmdbId, CancellationToken ct);

    /// <summary>
    /// TMDB <c>search/movie</c>: the first page of films matching <paramref name="title"/>, most relevant first; with
    /// <paramref name="year"/>, only films with a release in that year. Adult films excluded.
    /// </summary>
    Task<Result<IReadOnlyList<TmdbSearchResult>>> SearchFilmsAsync(string title, int? year, CancellationToken ct);
}

public sealed record TmdbFilmSummary(int TmdbId, string Title, int? ReleaseYear, double Popularity);

/// <summary>
/// Full TMDB details for one film. The last four fields are required parameters so no caller can drop them by accident;
/// each is null when TMDB doesn't provide it (<see cref="BackdropPath"/>, <see cref="VoteAverage"/>, <see cref="VoteCount"/>,
/// <see cref="OriginalLanguage"/>).
/// </summary>
public sealed record TmdbFilmDetails(
    int TmdbId,
    string Title,
    int? ReleaseYear,
    string? Overview,
    string? PosterPath,
    int? RuntimeMinutes,
    IReadOnlyList<string> Genres,
    double? Popularity,
    string? BackdropPath,
    double? VoteAverage,
    int? VoteCount,
    string? OriginalLanguage);

/// <summary>One <c>search/movie</c> result: enough to apply the import's matching rule (§13 job rule 2).</summary>
public sealed record TmdbSearchResult(int TmdbId, string Title, string? OriginalTitle, int? ReleaseYear, double Popularity);

public sealed record TmdbVideo(string Site, string Key, string Name);
