using Ithaca.Domain.Common;
using Ithaca.Domain.Errors;
using Ithaca.Domain.Library;

namespace Ithaca.Domain.Imports;

/// <summary>
/// One Letterboxd import of a user (glossary: <c>ImportJob</c>, api-contract.md §13, ADR-0024): its status, progress and
/// what it changed in My Cinema. The uploaded file is never stored; its parsed rows are the job's <see cref="ImportItem"/>s.
/// At most one job per user is <see cref="ImportStatus.Queued"/> or <see cref="ImportStatus.Running"/> (a partial unique index).
/// </summary>
public sealed class ImportJob
{
    private ImportJob(Guid userId, int filmCount, DateTimeOffset now)
    {
        Id = SequentialGuid.NewV7(now);
        UserId = userId;
        Status = ImportStatus.Queued;
        CreatedAt = now;
        FilmCount = filmCount;
        NotFoundListKept = true;
    }

    /// <summary>For persistence materialization only.</summary>
    private ImportJob()
    {
    }

    public Guid Id { get; private set; }

    public Guid UserId { get; private set; }

    public ImportStatus Status { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset? StartedAt { get; private set; }

    public DateTimeOffset? FinishedAt { get; private set; }

    /// <summary>Distinct films in the file, after merging the four files.</summary>
    public int FilmCount { get; private set; }

    public int ProcessedCount { get; private set; }

    public int MatchedCount { get; private set; }

    public int AddedToCatalogCount { get; private set; }

    public int NotFoundCount { get; private set; }

    /// <summary>False once a newer import replaced this one's not-found list (job rule 6); the counts stay.</summary>
    public bool NotFoundListKept { get; private set; }

    public int NewFilms { get; private set; }

    public int SeenSet { get; private set; }

    public int RatedSet { get; private set; }

    public int LikedSet { get; private set; }

    public int WatchlistSet { get; private set; }

    public int RatingsKept { get; private set; }

    /// <summary>Batches in a row that hit a write conflict and saved nothing; the job fails at <see cref="MaxConflictRetries"/>.</summary>
    public int ConflictRetries { get; private set; }

    /// <summary>A conflict that keeps coming back isn't a race: fail instead of blocking every later import.</summary>
    public const int MaxConflictRetries = 5;

    public bool IsActive => Status is ImportStatus.Queued or ImportStatus.Running;

    public static ImportJob Create(Guid userId, int filmCount, DateTimeOffset now)
    {
        if (userId == Guid.Empty)
        {
            throw new DomainException(ErrorCodes.Validation, "userId is required.");
        }

        if (filmCount <= 0)
        {
            throw new DomainException(ErrorCodes.Validation, "An import needs at least one film.");
        }

        return new ImportJob(userId, filmCount, now);
    }

    /// <summary>Queued → running. A running job (resumed after a restart) stays as it is.</summary>
    public void Start(DateTimeOffset now)
    {
        if (Status == ImportStatus.Queued)
        {
            Status = ImportStatus.Running;
            StartedAt = now;
        }
    }

    /// <summary>Adds one processed batch to the progress and change counts.</summary>
    public void RecordBatch(int processed, int matched, int addedToCatalog, int notFound, ImportChanges changes)
    {
        if (Status != ImportStatus.Running)
        {
            throw new DomainException(ErrorCodes.Validation, "Only a running import records progress.");
        }

        ProcessedCount = Math.Min(FilmCount, ProcessedCount + processed);
        MatchedCount += matched;
        AddedToCatalogCount += addedToCatalog;
        NotFoundCount += notFound;
        NewFilms += changes.NewFilm;
        SeenSet += changes.Seen;
        RatedSet += changes.Rated;
        LikedSet += changes.Liked;
        WatchlistSet += changes.Watchlist;
        RatingsKept += changes.RatingKept;
        ConflictRetries = 0;
    }

    /// <summary>Every film processed (not-found rows included).</summary>
    public void Complete(DateTimeOffset now)
    {
        Status = ImportStatus.Completed;
        FinishedAt = now;
    }
}
