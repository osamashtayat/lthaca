using System.Linq.Expressions;
using Ithaca.Application.Auth;
using Ithaca.Application.Common;
using Ithaca.Domain.Imports;
using Ithaca.Domain.Library;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

[assembly: System.Diagnostics.CodeAnalysis.SuppressMessage("Naming", "CA1716", Scope = "namespace", Target = "~N:Ithaca.Application.Imports", Justification = "C#-only codebase; the feature folder is Imports (backlog BE-15a).")]

namespace Ithaca.Application.Imports;

/// <summary>
/// Letterboxd imports (api-contract.md §13, ADR-0024). The request side acts for the signed-in user from
/// <see cref="ICurrentActor"/>: upload (checked and parsed in memory by <see cref="LetterboxdExportReader"/>, stored as an
/// <see cref="ImportJob"/> with its <see cref="ImportItem"/>s; the file itself is never stored), list, progress and the
/// not-found list. The job side is <see cref="ProcessNextBatchAsync"/>, called by <c>ImportJobRunner</c> in the background
/// (ADR-0003, ADR-0008): it matches one batch (<see cref="ImportFilmMatcher"/>), merges it into My Cinema (never remove,
/// any existing rating wins: <see cref="UserFilm.MergeImport"/>) and saves. <b>Resumable-batch exception</b> to "one save
/// per use case" (docs/backend-conventions.md): the job saves once per batch, so progress is visible while it runs and a
/// restart resumes from the first pending item; starting a job also replaces older imports' not-found lists with bulk
/// statements, and a failed job is marked with bulk statements (its tracked changes are abandoned).
/// </summary>
public sealed class ImportService(
    IAppDbContext db,
    ICurrentActor current,
    LetterboxdExportReader reader,
    ImportFilmMatcher matcher,
    IOptions<ImportOptions> options,
    TimeProvider time)
{
    private static readonly Expression<Func<ImportJob, ImportJobDto>> ToDto = j => new ImportJobDto(
        j.Id, j.Status, j.CreatedAt, j.FinishedAt, j.FilmCount, j.ProcessedCount, j.MatchedCount, j.AddedToCatalogCount,
        j.NotFoundCount, j.NotFoundListKept, new ImportChangesDto(j.NewFilms, j.SeenSet, j.RatedSet, j.LikedSet, j.WatchlistSet, j.RatingsKept));

    private static readonly Func<ImportJob, ImportJobDto> ToDtoCompiled = ToDto.Compile();

    /// <summary>Checks the upload in §13 order, then queues an <c>ImportJob</c>. One queued or running import per user.</summary>
    public async Task<Result<ImportJobDto>> CreateAsync(ImportUpload upload, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(upload);
        if (UserId() is not { } userId)
        {
            return AuthErrors.Unauthenticated;
        }

        var films = await reader.ReadAsync(upload, ct);
        if (!films.IsSuccess)
        {
            return films.Error;
        }

        if (await HasActiveImportAsync(userId, ct))
        {
            return ImportErrors.AlreadyRunning;
        }

        var now = time.GetUtcNow();
        var job = ImportJob.Create(userId, films.Value.Count, now);
        db.ImportJobs.Add(job);
        for (var position = 0; position < films.Value.Count; position++)
        {
            var film = films.Value[position];
            db.ImportItems.Add(ImportItem.Create(
                job.Id, position, film.Title, film.Year, film.LetterboxdUri, film.Watched, film.Liked, film.OnWatchlist, film.Rating, now));
        }

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (Exception e) when (WriteConflicts.IsWriteConflict(e))
        {
            // The only unique key a new job can hit is the one-active-import index: a concurrent upload won it.
            return await HasActiveImportAsync(userId, ct) ? ImportErrors.AlreadyRunning : throw new InvalidOperationException("Saving the import failed.", e);
        }

        return ToDtoCompiled(job);
    }

    /// <summary>The caller's imports, newest first.</summary>
    public async Task<Result<PagedResult<ImportJobDto>>> ListAsync(PageRequest page, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(page);
        if (UserId() is not { } userId)
        {
            return AuthErrors.Unauthenticated;
        }

        return await db.ImportJobs.AsNoTracking()
            .Where(j => j.UserId == userId)
            .OrderByDescending(j => j.CreatedAt)
            .ThenByDescending(j => j.Id)
            .Select(ToDto)
            .ToPagedResultAsync(page, ct);
    }

    public async Task<Result<ImportJobDto>> GetAsync(Guid importId, CancellationToken ct)
    {
        if (UserId() is not { } userId)
        {
            return AuthErrors.Unauthenticated;
        }

        var job = await db.ImportJobs.AsNoTracking().Where(j => j.Id == importId && j.UserId == userId).Select(ToDto).SingleOrDefaultAsync(ct);
        return job is null ? ImportErrors.NotFound : job;
    }

    /// <summary>Films the import couldn't match, by title then year; an empty page once a newer import replaced the list.</summary>
    public async Task<Result<PagedResult<ImportNotFoundDto>>> ListNotFoundAsync(Guid importId, PageRequest page, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(page);
        if (UserId() is not { } userId)
        {
            return AuthErrors.Unauthenticated;
        }

        if (!await db.ImportJobs.AnyAsync(j => j.Id == importId && j.UserId == userId, ct))
        {
            return ImportErrors.NotFound;
        }

        return await db.ImportItems.AsNoTracking()
            .Where(i => i.ImportJobId == importId && i.NotFoundReason != null)
            .OrderBy(i => i.Title)
            .ThenBy(i => i.Year)
            .ThenBy(i => i.Position)
            .Select(i => new ImportNotFoundDto(i.Title, i.Year, i.LetterboxdUri, i.NotFoundReason!.Value))
            .ToPagedResultAsync(page, ct);
    }

    /// <summary>
    /// The job: one batch of the oldest queued or running import (§13 job rules 2–6). Queued → running (replacing the
    /// user's older not-found lists); then up to <see cref="ImportOptions.BatchSize"/> pending items are matched, merged and
    /// saved with the progress; matched items are deleted, not-found ones kept as the list. No pending item left →
    /// <c>completed</c>. A write conflict (the user or the sync wrote the same row meanwhile) saves nothing and is retried
    /// on the next call; any other unexpected error marks the job <c>failed</c>, keeping what earlier batches wrote.
    /// </summary>
    public async Task<ImportBatchResult> ProcessNextBatchAsync(CancellationToken ct)
    {
        var job = await db.ImportJobs
            .Where(j => j.Status == ImportStatus.Queued || j.Status == ImportStatus.Running)
            .OrderBy(j => j.CreatedAt)
            .ThenBy(j => j.Id)
            .FirstOrDefaultAsync(ct);
        if (job is null)
        {
            return new ImportBatchResult(ImportBatchOutcome.Idle, null);
        }

        try
        {
            return new ImportBatchResult(await ProcessBatchAsync(job, ct), job.Id);
        }
        catch (Exception e) when (WriteConflicts.IsWriteConflict(e))
        {
            return new ImportBatchResult(ImportBatchOutcome.Retry, job.Id);
        }
        catch (Exception e) when (e is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            await FailAsync(job.Id, ct);
            return new ImportBatchResult(ImportBatchOutcome.Failed, job.Id);
        }
    }

    private async Task<ImportBatchOutcome> ProcessBatchAsync(ImportJob job, CancellationToken ct)
    {
        if (job.Status == ImportStatus.Queued)
        {
            job.Start(time.GetUtcNow());
            await ReplaceOlderNotFoundListsAsync(job, ct);
        }

        var items = await db.ImportItems
            .Where(i => i.ImportJobId == job.Id && i.NotFoundReason == null)
            .OrderBy(i => i.Position)
            .Take(options.Value.BatchSize)
            .ToListAsync(ct);
        if (items.Count == 0)
        {
            job.Complete(time.GetUtcNow());
            await db.SaveChangesAsync(ct);
            return ImportBatchOutcome.Completed;
        }

        var matches = await matcher.MatchAsync(items, ct);
        var now = time.GetUtcNow();
        var filmIds = matches.Where(m => m.FilmId is not null).Select(m => m.FilmId!.Value).Distinct().ToList();
        var library = await db.UserFilms
            .Where(u => u.UserId == job.UserId && filmIds.Contains(u.FilmId))
            .ToDictionaryAsync(u => u.FilmId, ct);
        var changes = default(ImportChanges);
        int matched = 0, added = 0, notFound = 0;
        foreach (var match in matches)
        {
            if (match.FilmId is not { } filmId)
            {
                match.Item.MarkNotFound(match.NotFound ?? ImportNotFoundReason.NoMatch);
                notFound++;
                continue;
            }

            var isNew = !library.TryGetValue(filmId, out var userFilm);
            if (userFilm is null)
            {
                userFilm = UserFilm.Create(job.UserId, filmId, now);
                db.UserFilms.Add(userFilm);
                library[filmId] = userFilm;
            }

            var item = match.Item;
            changes += userFilm.MergeImport(item.Watched, item.Liked, item.OnWatchlist, item.RatingHalfStars, now) with { NewFilm = isNew ? 1 : 0 };
            db.ImportItems.Remove(item);
            matched++;
            added += match.AddedToCatalog ? 1 : 0;
        }

        job.RecordBatch(items.Count, matched, added, notFound, changes);
        await db.SaveChangesAsync(ct);
        return ImportBatchOutcome.Progressed;
    }

    /// <summary>Job rule 6: only the user's latest import keeps its not-found list; older ones keep their counts.</summary>
    private async Task ReplaceOlderNotFoundListsAsync(ImportJob job, CancellationToken ct)
    {
        var older = db.ImportJobs.Where(j => j.UserId == job.UserId && j.Id != job.Id);
        await db.ImportItems.Where(i => older.Any(j => j.Id == i.ImportJobId)).ExecuteDeleteAsync(ct);
        await older.Where(j => j.NotFoundListKept).ExecuteUpdateAsync(s => s.SetProperty(j => j.NotFoundListKept, false), ct);
    }

    /// <summary>Bulk statements, so the abandoned tracked changes of the failed batch aren't saved with it.</summary>
    private async Task FailAsync(Guid jobId, CancellationToken ct)
    {
        var now = time.GetUtcNow();
        await db.ImportItems.Where(i => i.ImportJobId == jobId && i.NotFoundReason == null).ExecuteDeleteAsync(ct);
        await db.ImportJobs.Where(j => j.Id == jobId)
            .ExecuteUpdateAsync(s => s.SetProperty(j => j.Status, ImportStatus.Failed).SetProperty(j => j.FinishedAt, now), ct);
    }

    private Task<bool> HasActiveImportAsync(Guid userId, CancellationToken ct) =>
        db.ImportJobs.AnyAsync(j => j.UserId == userId && (j.Status == ImportStatus.Queued || j.Status == ImportStatus.Running), ct);

    private Guid? UserId() => current.Actor is { IsUser: true } actor ? actor.Id : null;
}

/// <summary>What one <see cref="ImportService.ProcessNextBatchAsync"/> call did, for the runner's pacing and logs (ids only).</summary>
public sealed record ImportBatchResult(ImportBatchOutcome Outcome, Guid? ImportId);

public enum ImportBatchOutcome
{
    /// <summary>No queued or running import: wait before looking again.</summary>
    Idle,

    /// <summary>A batch was saved; more may follow at once.</summary>
    Progressed,

    /// <summary>The import finished.</summary>
    Completed,

    /// <summary>A write conflict; nothing saved, the same batch is retried after a pause.</summary>
    Retry,

    /// <summary>An unexpected error; the import is marked failed.</summary>
    Failed,
}
