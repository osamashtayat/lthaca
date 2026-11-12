using Ithaca.Domain.Common;
using Ithaca.Domain.Errors;

namespace Ithaca.Domain.Films;

/// <summary>
/// A film in our own catalog, synced nightly from TMDB (ADR-0003). Runtime reads come from here, never from live TMDB.
/// <see cref="Embedding"/> is stored in a pgvector column (ADR-0002); Domain sees a plain <c>float[]</c>.
/// </summary>
public sealed class Film
{
    public const int TitleMaxLength = 300;

    /// <summary>Length of every embedding vector. Changing it needs a migration and a re-embed of the catalog.</summary>
    public const int EmbeddingDimensions = 1536;

    private Film(int tmdbId, string title, DateTimeOffset now)
    {
        Id = SequentialGuid.NewV7(now);
        TmdbId = tmdbId;
        Title = title;
        SyncedAt = now;
    }

    /// <summary>For persistence materialization only.</summary>
    private Film()
    {
        Title = "";
    }

    public Guid Id { get; private set; }

    /// <summary>TMDB's id: the public identifier in URLs and the only film id the LLM may return.</summary>
    public int TmdbId { get; private set; }

    public string Title { get; private set; }

    public int? ReleaseYear { get; private set; }

    public string? Overview { get; private set; }

    /// <summary>TMDB image path (e.g. <c>/abc.jpg</c>); the frontend builds the full URL.</summary>
    public string? PosterPath { get; private set; }

    public int? RuntimeMinutes { get; private set; }

    public string[] Genres { get; private set; } = [];

    public float[]? Embedding { get; private set; }

    public DateTimeOffset SyncedAt { get; private set; }

    public static Film Create(int tmdbId, string title, DateTimeOffset now)
    {
        if (tmdbId <= 0)
        {
            throw new DomainException(ErrorCodes.Validation, "tmdbId must be positive.", "tmdbId");
        }

        if (string.IsNullOrWhiteSpace(title) || title.Length > TitleMaxLength)
        {
            throw new DomainException(ErrorCodes.Validation, $"title must be 1-{TitleMaxLength} characters.", "title");
        }

        return new Film(tmdbId, title.Trim(), now);
    }

    /// <summary>Catalog metadata from a TMDB sync. Kept separate from <see cref="Create"/> so a re-sync can update in place.</summary>
    public void UpdateDetails(int? releaseYear, string? overview, string? posterPath, int? runtimeMinutes, string[] genres, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(genres);
        ReleaseYear = releaseYear;
        Overview = overview;
        PosterPath = posterPath;
        RuntimeMinutes = runtimeMinutes;
        Genres = genres;
        SyncedAt = now;
    }

    public void SetEmbedding(float[] embedding)
    {
        ArgumentNullException.ThrowIfNull(embedding);
        if (embedding.Length != EmbeddingDimensions)
        {
            throw new DomainException(ErrorCodes.Validation, $"embedding must have {EmbeddingDimensions} dimensions.", "embedding");
        }

        Embedding = embedding;
    }
}
