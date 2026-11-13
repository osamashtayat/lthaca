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

    /// <summary>Length of every embedding vector (Voyage 4-series default, ADR-0013). Changing it needs a migration and a re-embed.</summary>
    public const int EmbeddingDimensions = 1024;

    public const int EmbeddingModelMaxLength = 100;

    /// <summary>Length of a lowercase hex SHA-256 (<see cref="FilmFacts.HashOf"/>).</summary>
    public const int EmbeddingSourceHashLength = 64;

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

    /// <summary>TMDB popularity at the last sync; used for ordering (e.g. the "Just swipe" sampler).</summary>
    public double? Popularity { get; private set; }

    public float[]? Embedding { get; private set; }

    /// <summary>The model that produced <see cref="Embedding"/>; vectors from different models are never compared.</summary>
    public string? EmbeddingModel { get; private set; }

    /// <summary>
    /// Hash of the text <see cref="Embedding"/> was made from (<see cref="FilmFacts.EmbeddingTextHash"/> at the time). When it
    /// no longer matches the film's facts, or <see cref="EmbeddingModel"/> isn't the configured model, the sync re-embeds.
    /// </summary>
    public string? EmbeddingSourceHash { get; private set; }

    public DateTimeOffset SyncedAt { get; private set; }

    /// <summary>
    /// False once TMDB no longer knows the film (404 on refresh) while user data still references it (ADR-0019, A14): the
    /// row and our ids stay, the TMDB display fields are cleared. Set back to true if TMDB serves it again.
    /// </summary>
    public bool IsAvailable { get; private set; } = true;

    public static Film Create(int tmdbId, string title, DateTimeOffset now)
    {
        if (tmdbId <= 0)
        {
            throw new DomainException(ErrorCodes.Validation, "tmdbId must be positive.", "tmdbId");
        }

        return new Film(tmdbId, ValidTitle(title), now);
    }

    /// <summary>
    /// Catalog metadata from a TMDB sync, for display only (ADR-0017: none of it is embedded); also refreshes
    /// <see cref="SyncedAt"/> (TMDB's 6-month caching limit is counted from it).
    /// </summary>
    public void UpdateDetails(
        string title, int? releaseYear, string? overview, string? posterPath, int? runtimeMinutes, string[] genres, double? popularity, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(genres);
        Title = ValidTitle(title);
        ReleaseYear = releaseYear;
        Overview = overview;
        PosterPath = posterPath;
        RuntimeMinutes = runtimeMinutes;
        Genres = genres;
        Popularity = popularity;
        IsAvailable = true;
        SyncedAt = now;
    }

    /// <summary>
    /// TMDB answered 404 for a film user data references (ADR-0019): keep the row, clear the TMDB-derived display fields
    /// (overview, poster), mark it unavailable. <see cref="SyncedAt"/> is refreshed: we now hold nothing TMDB asked us to
    /// drop, and referenced films are never purged anyway.
    /// </summary>
    public void MarkUnavailable(DateTimeOffset now)
    {
        Overview = null;
        PosterPath = null;
        IsAvailable = false;
        SyncedAt = now;
    }

    /// <summary>Stores a vector with the model that made it and the hash of the text it was made from.</summary>
    public void SetEmbedding(float[] embedding, string model, string sourceHash)
    {
        ArgumentNullException.ThrowIfNull(embedding);
        ArgumentException.ThrowIfNullOrWhiteSpace(model);
        if (sourceHash is not { Length: EmbeddingSourceHashLength })
        {
            throw new DomainException(ErrorCodes.Validation, "sourceHash must be a SHA-256 hex string.", "sourceHash");
        }

        if (embedding.Length != EmbeddingDimensions)
        {
            throw new DomainException(ErrorCodes.Validation, $"embedding must have {EmbeddingDimensions} dimensions.", "embedding");
        }

        EmbeddingModel = model.Length <= EmbeddingModelMaxLength
            ? model
            : throw new DomainException(ErrorCodes.Validation, "embedding model name is too long.", "embeddingModel");
        Embedding = embedding;
        EmbeddingSourceHash = sourceHash;
    }

    /// <summary>The one title rule: 1–<see cref="TitleMaxLength"/> characters after trimming.</summary>
    public static bool IsValidTitle(string? title) => !string.IsNullOrWhiteSpace(title) && title.Trim().Length <= TitleMaxLength;

    private static string ValidTitle(string title) =>
        IsValidTitle(title)
            ? title.Trim()
            : throw new DomainException(ErrorCodes.Validation, $"title must be 1-{TitleMaxLength} characters.", "title");
}
