namespace Ithaca.Application.Films;

/// <summary>
/// <c>GET /api/films/{tmdbId}</c> response. Never return the <c>Film</c> entity itself. <see cref="BackdropPath"/>,
/// <see cref="VoteAverage"/>, <see cref="VoteCount"/> and <see cref="OriginalLanguage"/> are null until the sync has stored them.
/// </summary>
public sealed record FilmDto(
    int TmdbId,
    string Title,
    int? ReleaseYear,
    string? Overview,
    string? PosterPath,
    int? RuntimeMinutes,
    IReadOnlyList<string> Genres,
    string? BackdropPath,
    double? VoteAverage,
    int? VoteCount,
    string? OriginalLanguage);

/// <summary>A film in a list (browse, library, results): no overview, backdrop, vote count or language.</summary>
public sealed record FilmSummaryDto(
    int TmdbId,
    string Title,
    int? ReleaseYear,
    string? PosterPath,
    int? RuntimeMinutes,
    IReadOnlyList<string> Genres,
    double? VoteAverage);
