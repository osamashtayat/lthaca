namespace Ithaca.Application.Tmdb;

/// <summary>
/// TMDB access. Callers: the nightly catalog sync and the (cached) details/trailer lookup. Never used to build decks:
/// candidates come from our own <c>films</c> table (ADR-0003).
/// </summary>
public interface ITmdbClient
{
    /// <summary>One page of the catalog to sync (e.g. TMDB discover, popularity-ordered). Empty when past the last page.</summary>
    Task<IReadOnlyList<TmdbFilmSummary>> GetCatalogPageAsync(int page, CancellationToken ct);

    /// <summary>Full details for one film, or null when TMDB doesn't know it.</summary>
    Task<TmdbFilmDetails?> GetFilmDetailsAsync(int tmdbId, CancellationToken ct);

    Task<IReadOnlyList<TmdbVideo>> GetTrailersAsync(int tmdbId, CancellationToken ct);
}

public sealed record TmdbFilmSummary(int TmdbId, string Title, int? ReleaseYear, double Popularity);

public sealed record TmdbFilmDetails(
    int TmdbId, string Title, int? ReleaseYear, string? Overview, string? PosterPath, int? RuntimeMinutes, IReadOnlyList<string> Genres);

public sealed record TmdbVideo(string Site, string Key, string Name);
