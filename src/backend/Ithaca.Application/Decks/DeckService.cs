using Ithaca.Application.Auth;
using Ithaca.Application.Common;
using Ithaca.Application.Films;
using Ithaca.Application.Moods;
using Ithaca.Domain.Decks;
using Ithaca.Domain.Films;
using Ithaca.Domain.Library;
using Ithaca.Domain.Moods;
using Microsoft.EntityFrameworkCore;

namespace Ithaca.Application.Decks;

/// <summary>
/// Solo decks and swipes (api-contract.md §5), for the signed-in user from <see cref="ICurrentActor"/>; another user's deck
/// is <c>deck.not_found</c>. "Just swipe" (BE-08): the first batch is the <see cref="DeckSampler"/>'s varied sampler; later
/// batches adapt to this deck's swipes and the user's library. "By mood" (BE-09): the <see cref="MoodInterpreter"/> turns the
/// input into a <see cref="MoodQuery"/>; its filters narrow every batch, and its genres count as a strong signal. Films come
/// from our own catalog only (ADR-0003): available, not yet in this deck, not seen by the user.
/// </summary>
public sealed class DeckService(IAppDbContext db, ICurrentActor current, MoodInterpreter moods, TimeProvider time)
{
    /// <summary>Most popular eligible films the sampler chooses from per batch.</summary>
    private const int CandidatePool = 500;

    /// <summary>Most recently changed library rows used as signals.</summary>
    private const int LibrarySignalLimit = 200;

    private const int TopPicks = 3;

    /// <summary>Tries at dealing a batch when concurrent requests for the same deck collide.</summary>
    private const int DealAttempts = 3;

    /// <summary>How much a deck's mood genres count, next to one right swipe (1).</summary>
    private const double MoodSignalWeight = 2;

    public async Task<Result<DeckDto>> CreateAsync(CreateDeckRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (UserId() is not { } userId)
        {
            return AuthErrors.Unauthenticated;
        }

        if (request.Source == DeckSource.Taste)
        {
            return ContractStub.NotImplemented; // decks by taste: BE-14 (api-contract.md §12)
        }

        MoodQuery? mood = null;
        switch (request.Source, request.Mood)
        {
            case (DeckSource.Mood, null):
                return Error.Validation("mood", "A deck by mood needs a mood.");
            case (DeckSource.Mood, { } input):
                var interpreted = await moods.InterpretAsync(userId, input, ct);
                if (!interpreted.IsSuccess)
                {
                    return interpreted.Error; // nothing is created (ADR-0022)
                }

                mood = interpreted.Value;
                db.MoodQueries.Add(mood);
                break;
            case (_, not null):
                return Error.Validation("mood", "A mood only goes with a deck by mood.");
        }

        var now = time.GetUtcNow();
        var deck = Deck.Create(userId, request.Source, mood?.Id, now);
        db.Decks.Add(deck);
        await DealAsync(deck.Id, userId, dealt: 0, MoodSignals(mood), mood, now, ct);
        await db.SaveChangesAsync(ct);

        return await DeckDtoAsync(deck.Id, userId, ct) ?? throw new InvalidOperationException("The deck just created is missing.");
    }

    public async Task<Result<DeckDto>> GetAsync(Guid deckId, CancellationToken ct)
    {
        if (UserId() is not { } userId)
        {
            return AuthErrors.Unauthenticated;
        }

        return await DeckDtoAsync(deckId, userId, ct) is { } deck ? deck : DeckErrors.NotFound;
    }

    /// <summary>The next batch of cards, adapted to the swipes so far; an empty list means nothing is left.</summary>
    public async Task<Result<IReadOnlyList<DeckItemDto>>> AddItemsAsync(Guid deckId, CancellationToken ct)
    {
        if (UserId() is not { } userId)
        {
            return AuthErrors.Unauthenticated;
        }

        if (!await OwnsDeckAsync(deckId, userId, ct))
        {
            return DeckErrors.NotFound;
        }

        var mood = await MoodOfAsync(deckId, ct);
        var signals = MoodSignals(mood).Concat(await SignalsAsync(deckId, userId, ct)).ToList();
        for (var attempt = 1; ; attempt++)
        {
            var dealt = await db.DeckItems.CountAsync(i => i.DeckId == deckId, ct);
            var added = await DealAsync(deckId, userId, dealt, signals, mood, time.GetUtcNow(), ct);
            if (added.Count == 0)
            {
                return Array.Empty<DeckItemDto>();
            }

            try
            {
                await db.SaveChangesAsync(ct);
                var ids = added.Select(i => i.Id).ToList();
                return Result<IReadOnlyList<DeckItemDto>>.Success(await ItemDtosAsync(db.DeckItems.Where(i => ids.Contains(i.Id)), userId, ct));
            }
            catch (Exception e) when (WriteConflicts.IsWriteConflict(e) && attempt < DealAttempts)
            {
                // A concurrent request for the same deck took these positions or films: drop this batch and deal again.
                db.DeckItems.RemoveRange(added);
            }
        }
    }

    /// <summary>
    /// Records a swipe. The same <c>swipeId</c> again is a no-op success; another swipe on a swiped card is
    /// <c>deck.already_swiped</c>. "Up" marks the film seen in the library, and liked with "loved" (§5); right and left
    /// don't touch the library (A2). Known gap: if the same film's library row is created by another request at the same
    /// moment (an "up" in another deck, or a library <c>PUT</c>), the unique key rejects this save and it is a 500.
    /// </summary>
    public async Task<Result> SwipeAsync(Guid deckId, SwipeRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (UserId() is not { } userId)
        {
            return AuthErrors.Unauthenticated;
        }

        if (!Swipe.IsValidVerdict(request.Direction, request.SeenVerdict))
        {
            return Error.Validation("seenVerdict", "A verdict only goes with an \"up\" swipe.");
        }

        if (!await OwnsDeckAsync(deckId, userId, ct))
        {
            return DeckErrors.NotFound;
        }

        if (await RetriedAsync(request, deckId, ct) is { } retried)
        {
            return retried;
        }

        var item = await db.DeckItems.AsNoTracking()
            .Where(i => i.Id == request.DeckItemId && i.DeckId == deckId)
            .Select(i => new { i.Id, i.FilmId })
            .SingleOrDefaultAsync(ct);
        if (item is null)
        {
            return DeckErrors.ItemNotFound;
        }

        if (await db.Swipes.AnyAsync(s => s.DeckItemId == item.Id, ct))
        {
            return DeckErrors.AlreadySwiped;
        }

        var now = time.GetUtcNow();
        db.Swipes.Add(Swipe.Create(request.SwipeId, item.Id, request.Direction, request.SeenVerdict, now));
        if (request.Direction == SwipeDirection.Up)
        {
            var row = await db.UserFilms.SingleOrDefaultAsync(u => u.UserId == userId && u.FilmId == item.FilmId, ct);
            if (row is null)
            {
                row = UserFilm.Create(userId, item.FilmId, now);
                db.UserFilms.Add(row);
            }

            row.MarkSeen(request.SeenVerdict == SeenVerdict.Loved, now);
        }

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (Exception e) when (WriteConflicts.IsWriteConflict(e))
        {
            // A concurrent request swiped this card first: the same swipeId is a retry, another one a conflict.
            if (await RetriedAsync(request, deckId, ct) is { } raced)
            {
                return raced;
            }

            if (await db.Swipes.AnyAsync(s => s.DeckItemId == item.Id, ct))
            {
                return DeckErrors.AlreadySwiped;
            }

            throw;
        }

        return Result.Success();
    }

    /// <summary>Up to three of the deck's right swipes, best genre fit with the deck's swipes first, then TMDB popularity.</summary>
    public async Task<Result<DeckTopDto>> GetTopAsync(Guid deckId, CancellationToken ct)
    {
        if (UserId() is not { } userId)
        {
            return AuthErrors.Unauthenticated;
        }

        if (!await OwnsDeckAsync(deckId, userId, ct))
        {
            return DeckErrors.NotFound;
        }

        var swipes = await SwipesOf(deckId)
            .Select(x => new { x.Swipe.Direction, x.Film.Id, x.Film.TmdbId, x.Film.Genres, x.Film.ReleaseYear, x.Film.RuntimeMinutes, x.Film.Popularity, x.Item.Position })
            .ToListAsync(ct);
        var yeses = swipes.Where(s => s.Direction == SwipeDirection.Right).OrderBy(s => s.Position).ToList();
        if (yeses.Count == 0)
        {
            return DeckErrors.NoYeses;
        }

        var signals = MoodSignals(await MoodOfAsync(deckId, ct)).Concat(await SignalsAsync(deckId, userId, ct, includeEmbeddings: false)).ToList();
        var ranked = DeckSampler.RankByGenreFit(
            yeses.Select(y => new SamplerFilm(y.Id, y.Genres, y.ReleaseYear, y.RuntimeMinutes, y.Popularity ?? 0, null)).ToList(),
            signals.Select(sig => sig.ToDeckSignal(model: null)).ToList());
        var tmdbIds = yeses.ToDictionary(y => y.Id, y => y.TmdbId);
        var best = ranked.Take(TopPicks).Select(id => tmdbIds[id]).ToList();
        var films = await FilmDtosAsync(best, ct);

        return new DeckTopDto(swipes.Count, yeses.Count, best.Select(id => new TopPickDto(films[id], null)).ToList());
    }

    /// <summary>
    /// Adds up to <see cref="Deck.BatchSize"/> new items (positions after <paramref name="dealt"/>), never past
    /// <see cref="Deck.MaxItems"/>. Not saved here: the caller saves once.
    /// </summary>
    private async Task<List<DeckItem>> DealAsync(
        Guid deckId, Guid userId, int dealt, IReadOnlyList<SignalRow> signals, MoodQuery? mood, DateTimeOffset now, CancellationToken ct)
    {
        var count = Math.Min(Deck.BatchSize, Deck.MaxItems - dealt);
        if (count <= 0)
        {
            return [];
        }

        // Vectors from different models are never compared: use the model most signals were embedded with.
        var model = signals.Where(s => s.Embedding is not null).GroupBy(s => s.EmbeddingModel).MaxBy(g => g.Count())?.Key;
        var inDeck = db.DeckItems.Where(i => i.DeckId == deckId).Select(i => i.FilmId);
        var seen = db.UserFilms.Where(u => u.UserId == userId && u.Seen).Select(u => u.FilmId);
        var candidates = await Filtered(db.Films.AsNoTracking(), mood)
            .Where(f => f.IsAvailable && !inDeck.Contains(f.Id) && !seen.Contains(f.Id))
            .OrderBy(f => f.Popularity == null).ThenByDescending(f => f.Popularity).ThenBy(f => f.TmdbId)
            .Take(CandidatePool)
            .Select(f => new SamplerFilm(
                f.Id, f.Genres, f.ReleaseYear, f.RuntimeMinutes, f.Popularity ?? 0,
                model != null && f.EmbeddingModel == model ? f.Embedding : null))
            .ToListAsync(ct);

        var deckSignals = signals.Select(s => s.ToDeckSignal(model)).ToList();
        var picked = DeckSampler.Pick(candidates, deckSignals, count, Random.Shared);
        var items = picked.Select((filmId, i) => DeckItem.Create(deckId, filmId, dealt + i + 1, now)).ToList();
        db.DeckItems.AddRange(items);
        return items;
    }

    /// <summary>This deck's swipes (full weight) and the user's most recent library rows (<see cref="DeckSignals"/>).</summary>
    private async Task<List<SignalRow>> SignalsAsync(Guid deckId, Guid userId, CancellationToken ct, bool includeEmbeddings = true)
    {
        var swiped = await SwipesOf(deckId)
            .Select(x => new
            {
                x.Swipe.Direction,
                x.Swipe.SeenVerdict,
                x.Film.Genres,
                x.Film.RuntimeMinutes,
                Embedding = includeEmbeddings ? x.Film.Embedding : null,
                x.Film.EmbeddingModel,
            })
            .ToListAsync(ct);

        var swipedFilms = SwipesOf(deckId).Select(x => x.Film.Id);
        var library = await db.UserFilms.AsNoTracking()
            .Where(u => u.UserId == userId && (u.Liked || u.OnWatchlist || u.Rating != null) && !swipedFilms.Contains(u.FilmId))
            .OrderByDescending(u => u.UpdatedAt)
            .Take(LibrarySignalLimit)
            .Join(db.Films, u => u.FilmId, f => f.Id, (u, f) => new
            {
                u.Liked,
                u.OnWatchlist,
                u.Rating,
                f.Genres,
                f.RuntimeMinutes,
                Embedding = includeEmbeddings ? f.Embedding : null,
                f.EmbeddingModel,
            })
            .ToListAsync(ct);

        return swiped
            .Select(s => new SignalRow(s.Genres, s.RuntimeMinutes, s.Embedding, s.EmbeddingModel, DeckSignals.OfSwipe(s.Direction, s.SeenVerdict)))
            .Concat(library.Select(l => new SignalRow(l.Genres, l.RuntimeMinutes, l.Embedding, l.EmbeddingModel, DeckSignals.OfLibrary(l.Liked, l.OnWatchlist, l.Rating))))
            .Where(s => s.Weight != 0)
            .ToList();
    }

    private IQueryable<SwipedFilm> SwipesOf(Guid deckId) =>
        from i in db.DeckItems.AsNoTracking()
        where i.DeckId == deckId
        join s in db.Swipes.AsNoTracking() on i.Id equals s.DeckItemId
        join f in db.Films.AsNoTracking() on i.FilmId equals f.Id
        select new SwipedFilm { Item = i, Swipe = s, Film = f };

    /// <summary>Success when this <c>swipeId</c> is already recorded on this card; a conflict when it was used for another; null when new.</summary>
    private async Task<Result?> RetriedAsync(SwipeRequest request, Guid deckId, CancellationToken ct)
    {
        var existing = await db.Swipes.AsNoTracking()
            .Where(s => s.Id == request.SwipeId)
            .Select(s => new { s.DeckItemId })
            .SingleOrDefaultAsync(ct);
        if (existing is null)
        {
            return null;
        }

        return existing.DeckItemId == request.DeckItemId && await db.DeckItems.AnyAsync(i => i.Id == existing.DeckItemId && i.DeckId == deckId, ct)
            ? Result.Success()
            : DeckErrors.AlreadySwiped;
    }

    private Task<bool> OwnsDeckAsync(Guid deckId, Guid userId, CancellationToken ct) =>
        db.Decks.AnyAsync(d => d.Id == deckId && d.UserId == userId, ct);

    private async Task<DeckDto?> DeckDtoAsync(Guid deckId, Guid userId, CancellationToken ct)
    {
        var deck = await db.Decks.AsNoTracking()
            .Where(d => d.Id == deckId && d.UserId == userId)
            .Select(d => new { d.Id, d.Source, d.CreatedAt })
            .SingleOrDefaultAsync(ct);
        if (deck is null)
        {
            return null;
        }

        var mood = await MoodOfAsync(deckId, ct);
        var items = await ItemDtosAsync(db.DeckItems.Where(i => i.DeckId == deckId), userId, ct);
        return new DeckDto(
            deck.Id, deck.Source, deck.CreatedAt, mood is null ? null : MoodQueryDtos.From(mood), items,
            items.Count(i => i.Swipe is not null), items.Count(i => i.Swipe?.Direction == SwipeDirection.Right));
    }

    private async Task<MoodQuery?> MoodOfAsync(Guid deckId, CancellationToken ct) =>
        await db.Decks.AsNoTracking()
            .Where(d => d.Id == deckId && d.MoodQueryId != null)
            .Join(db.MoodQueries.AsNoTracking(), d => d.MoodQueryId, m => m.Id, (d, m) => m)
            .SingleOrDefaultAsync(ct);

    /// <summary>A deck's mood genres as one strong signal, so batches and the top 3 lean towards them.</summary>
    private static List<SignalRow> MoodSignals(MoodQuery? mood) =>
        mood is { IncludeGenres.Length: > 0 } ? [new SignalRow(mood.IncludeGenres, null, null, null, MoodSignalWeight)] : [];

    /// <summary>
    /// A mood's filters as hard limits: any included genre, no excluded genre, runtime, release years, original language.
    /// A film missing the value a filter needs doesn't pass it.
    /// </summary>
    private static IQueryable<Film> Filtered(IQueryable<Film> films, MoodQuery? mood)
    {
        if (mood is null)
        {
            return films;
        }

        if (mood.IncludeGenres is { Length: > 0 } include)
        {
            films = films.Where(f => f.Genres.Any(g => include.Contains(g)));
        }

        if (mood.ExcludeGenres is { Length: > 0 } exclude)
        {
            films = films.Where(f => !f.Genres.Any(g => exclude.Contains(g)));
        }

        if (mood.MaxRuntimeMinutes is { } minutes)
        {
            films = films.Where(f => f.RuntimeMinutes <= minutes);
        }

        if (mood.MinReleaseYear is { } from)
        {
            films = films.Where(f => f.ReleaseYear >= from);
        }

        if (mood.MaxReleaseYear is { } to)
        {
            films = films.Where(f => f.ReleaseYear <= to);
        }

        if (mood.Language is { } language)
        {
            films = films.Where(f => f.OriginalLanguage == language);
        }

        return films;
    }

    /// <summary>Items in position order with their film, the caller's swipe and the caller's watchlist flag.</summary>
    private async Task<IReadOnlyList<DeckItemDto>> ItemDtosAsync(IQueryable<DeckItem> items, Guid userId, CancellationToken ct)
    {
        var rows = await (
            from i in items.AsNoTracking()
            join f in db.Films.AsNoTracking() on i.FilmId equals f.Id
            join s in db.Swipes.AsNoTracking() on i.Id equals s.DeckItemId into swipes
            from s in swipes.DefaultIfEmpty()
            orderby i.Position
            select new
            {
                i.Id,
                i.Position,
                f.TmdbId,
                Direction = s == null ? (SwipeDirection?)null : s.Direction,
                Verdict = s == null ? null : s.SeenVerdict,
                OnWatchlist = db.UserFilms.Any(u => u.UserId == userId && u.FilmId == i.FilmId && u.OnWatchlist),
            })
            .ToListAsync(ct);

        var films = await FilmDtosAsync(rows.Select(r => r.TmdbId).ToList(), ct);

        return rows
            .Select(r => new DeckItemDto(
                r.Id, r.Position, films[r.TmdbId],
                r.Direction is { } direction ? new SwipeDto(direction, r.Verdict) : null,
                r.OnWatchlist))
            .ToList();
    }

    private async Task<Dictionary<int, FilmDto>> FilmDtosAsync(List<int> tmdbIds, CancellationToken ct) =>
        await db.Films.AsNoTracking()
            .Where(f => tmdbIds.Contains(f.TmdbId))
            .Select(FilmProjections.Dto)
            .ToDictionaryAsync(f => f.TmdbId, ct);

    private Guid? UserId() => current.Actor is { IsUser: true } actor ? actor.Id : null;

    /// <summary>A class with init properties (not a positional record) so EF can translate member access through it.</summary>
    private sealed class SwipedFilm
    {
        public required DeckItem Item { get; init; }

        public required Swipe Swipe { get; init; }

        public required Film Film { get; init; }
    }

    private sealed record SignalRow(IReadOnlyList<string> Genres, int? RuntimeMinutes, float[]? Embedding, string? EmbeddingModel, double Weight)
    {
        /// <summary>The sampler's view; the embedding only when it comes from <paramref name="model"/>.</summary>
        public DeckSignal ToDeckSignal(string? model) =>
            new(Genres, RuntimeMinutes, model is not null && EmbeddingModel == model ? Embedding : null, Weight);
    }
}
