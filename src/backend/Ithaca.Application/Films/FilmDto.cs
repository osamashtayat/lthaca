namespace Ithaca.Application.Films;

/// <summary><c>GET /api/films/{tmdbId}</c> response. Never return the <c>Film</c> entity itself.</summary>
public sealed record FilmDto(
    int TmdbId,
    string Title,
    int? ReleaseYear,
    string? Overview,
    string? PosterPath,
    int? RuntimeMinutes,
    IReadOnlyList<string> Genres);
