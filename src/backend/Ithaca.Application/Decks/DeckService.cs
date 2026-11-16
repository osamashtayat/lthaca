using Ithaca.Application.Auth;
using Ithaca.Application.Common;
using Ithaca.Application.Films;
using Ithaca.Application.Moods;
using Ithaca.Application.Taste;
using Ithaca.Domain.Decks;
using Ithaca.Domain.Films;
using Ithaca.Domain.Library;
using Ithaca.Domain.Moods;
using Ithaca.Domain.Taste;
using Microsoft.EntityFrameworkCore;

namespace Ithaca.Application.Decks;

/// <summary>
/// Solo decks and swipes (api-contract.md §5), for the signed-in user from <see cref="ICurrentActor"/>; another user's deck
/// is <c>deck.not_found</c>. "Just swipe" (BE-08): the first batch is the <see cref="DeckSampler"/>'s varied sampler; later
/// batches adapt to this deck's swipes and the user's library. "By mood" (BE-09): the <see cref="MoodInterpreter"/> turns the
/// input into a <see cref="MoodQuery"/>; its filters narrow every batch, and its genres count as a strong signal. "By taste"
/// (BE-14): like "Just swipe", but every batch leans towards the user's <see cref="TasteProfile"/>; only for users with enough
/// history (<c>taste.not_ready</c>). Films come from our own catalog only (ADR-0003): available, not yet in this deck, not
/// seen by the user.
/// </summary>
public sealed class DeckService(
    IAppDbContext db, ICurrentActor current, MoodInterpreter moods, TasteProfileReader tastes, DeckDealer dealer, DeckItemReader cards, TimeProvider time)
{
    /// <summary>Most recently changed library rows used as signals.</summary>
    private const int LibrarySignalLimit = 200;

    private const int TopPicks = 3;

    /// <summary>Tries at dealing a batch when concurrent requests for the same deck collide.</summary>
    private const int DealAttempts = 3;

    public async Task<Result<DeckDto>> CreateAsync(CreateDeckRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (UserId() is not { } userId)
        {
            return AuthErrors.Unauthenticated;
        }

        MoodQuery? mood = null;
        TasteProfile? taste = null;
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
            case (DeckSource.Taste, null):
                if (!await tastes.IsReadyAsync(userId, ct))
                {
                    return TasteErrors.NotReady; // after input validation (§12); nothing is created
                }

                taste = await tastes.ProfileAsync(userId, ct);
                break;
        }

        var now = time.GetUtcNow();
        var deck = Deck.Create(userId, request.Source, mood?.Id, now);
        db.Decks.Add(deck);
        await DealAsync(deck.Id, userId, dealt: 0, MoodSignals(mood), mood, taste, now, ct);
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

        if (await SourceOfAsync(deckId, userId, ct) is not { } source)
        {
            return DeckErrors.NotFound;
        }

        var mood = await MoodOfAsync(deckId, ct);
        var taste = source == DeckSource.Taste ? await tastes.ProfileAsync(userId, ct) : null;
        var signals = MoodSignals(mood).Concat(await SignalsAsync(deckId, userId, ct)).ToList();
        for (var attempt = 1; ; attempt++)
        {
            var dealt = await db.DeckItems.CountAsync(i => i.DeckId == deckId, ct);
            var added = await DealAsync(deckId, userId, dealt, signals, mood, taste, time.GetUtcNow(), ct);
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
            return DeckErrors.VerdictOnlyWithUp;
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
        var films = await cards.FilmDtosAsync(best, ct);

        return new DeckTopDto(swipes.Count, yeses.Count, best.Select(id => new TopPickDto(films[id], null)).ToList());
    }

    /// <summary>
    /// Adds up to <see cref="Deck.BatchSize"/> new items (positions after <paramref name="dealt"/>), never past
    /// <see cref="Deck.MaxItems"/> nor a film the user has seen; leaning towards <paramref name="taste"/> when given. Not saved
    /// here: the caller saves once.
    /// </summary>
    private Task<List<DeckItem>> DealAsync(
        Guid deckId, Guid userId, int dealt, IReadOnlyList<SignalRow> signals, MoodQuery? mood, TasteProfile? taste, DateTimeOffset now, CancellationToken ct)
    {
        var count = Math.Min(Deck.BatchSize, Deck.MaxItems - dealt);
        if (count <= 0)
        {
            return Task.FromResult(new List<DeckItem>());
        }

        // Vectors from different models are never compared: the taste profile's model, else the one most signals were embedded with.
        var model = taste?.EmbeddingModel ?? signals.Where(s => s.Embedding is not null).GroupBy(s => s.EmbeddingModel).MaxBy(g => g.Count())?.Key;
        var seen = db.UserFilms.Where(u => u.UserId == userId && u.Seen).Select(u => u.FilmId);
        return dealer.DealAsync(
            deckId, dealt, count, [DeckFilter.FromMood(mood)], seen, signals.Select(s => s.ToDeckSignal(model)).ToList(), model, taste, now, ct);
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
            .Where(u => u.UserId == userId && (u.Liked || u.OnWatchlist || u.RatingHalfStars != null) && !swipedFilms.Contains(u.FilmId))
            .OrderByDescending(u => u.UpdatedAt)
            .Take(LibrarySignalLimit)
            .Join(db.Films, u => u.FilmId, f => f.Id, (u, f) => new
            {
                u.Liked,
                u.OnWatchlist,
                u.RatingHalfStars,
                f.Genres,
                f.RuntimeMinutes,
                Embedding = includeEmbeddings ? f.Embedding : null,
                f.EmbeddingModel,
            })
            .ToListAsync(ct);

        return swiped
            .Select(s => new SignalRow(s.Genres, s.RuntimeMinutes, s.Embedding, s.EmbeddingModel, DeckSignals.OfSwipe(s.Direction, s.SeenVerdict)))
            .Concat(library.Select(l => new SignalRow(l.Genres, l.RuntimeMinutes, l.Embedding, l.EmbeddingModel, DeckSignals.OfLibrary(l.Liked, l.OnWatchlist, UserFilm.ToRating(l.RatingHalfStars)))))
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

    /// <summary>The deck's source when it is the user's; null otherwise.</summary>
    private async Task<DeckSource?> SourceOfAsync(Guid deckId, Guid userId, CancellationToken ct) =>
        await db.Decks.AsNoTracking().Where(d => d.Id == deckId && d.UserId == userId).Select(d => (DeckSource?)d.Source).SingleOrDefaultAsync(ct);

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
        mood is { IncludeGenres.Length: > 0 } ? [new SignalRow(mood.IncludeGenres, null, null, null, DeckSignals.MoodWeight)] : [];

    /// <summary>The deck's items with the user's own swipes.</summary>
    private Task<IReadOnlyList<DeckItemDto>> ItemDtosAsync(IQueryable<DeckItem> items, Guid userId, CancellationToken ct) =>
        cards.ItemDtosAsync(
            items,
            db.Swipes.AsNoTracking().Select(s => new CardGesture { DeckItemId = s.DeckItemId, Direction = s.Direction, SeenVerdict = s.SeenVerdict }),
            userId,
            ct);

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
