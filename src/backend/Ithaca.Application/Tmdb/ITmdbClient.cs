using Ithaca.Application.Common;

namespace Ithaca.Application.Tmdb;

/// <summary>
/// TMDB access. Callers: the nightly catalog sync and (later) cached details such as trailers. Never used to build decks:
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
}

public sealed record TmdbFilmSummary(int TmdbId, string Title, int? ReleaseYear, double Popularity);

public sealed record TmdbFilmDetails(
    int TmdbId,
    string Title,
    int? ReleaseYear,
    string? Overview,
    string? PosterPath,
    int? RuntimeMinutes,
    IReadOnlyList<string> Genres,
    double? Popularity,
    string? BackdropPath = null,
    double? VoteAverage = null,
    int? VoteCount = null,
    string? OriginalLanguage = null);

public sealed record TmdbVideo(string Site, string Key, string Name);
