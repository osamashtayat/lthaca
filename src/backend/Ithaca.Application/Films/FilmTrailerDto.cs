using System.Text.Json.Serialization;

namespace Ithaca.Application.Films;

/// <summary>Where a trailer is hosted. TMDB's spelling, kept on the wire.</summary>
public enum TrailerSite
{
    [JsonStringEnumMemberName("YouTube")]
    YouTube,
}

/// <summary>A playable trailer.</summary>
public sealed record TrailerDto(TrailerSite Site, string Key, string Name);

/// <summary><c>GET /api/films/{tmdbId}/trailer</c>: <see cref="Trailer"/> is null when the film has none.</summary>
public sealed record FilmTrailerDto(TrailerDto? Trailer);
