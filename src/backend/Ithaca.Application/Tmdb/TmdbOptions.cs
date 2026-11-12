using System.ComponentModel.DataAnnotations;
using Ithaca.Application.Common;

namespace Ithaca.Application.Tmdb;

public enum TmdbClientKind
{
    Fake,
    Http,
}

/// <summary><c>Tmdb</c> section. Only the nightly sync and cached details (trailers) call TMDB (ADR-0003).</summary>
public sealed class TmdbOptions : IConfigSection
{
    public static string SectionName => "Tmdb";

    public TmdbClientKind Client { get; init; } = TmdbClientKind.Fake;

    [Required]
    [Url]
    public string BaseUrl { get; init; } = "https://api.themoviedb.org/3/";

    /// <summary>Secret: env var / user-secrets only (<c>Tmdb__ApiKey</c>).</summary>
    public string? ApiKey { get; init; }
}
