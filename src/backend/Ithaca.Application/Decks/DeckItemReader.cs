using Ithaca.Application.Common;
using Ithaca.Application.Films;
using Ithaca.Domain.Decks;
using Microsoft.EntityFrameworkCore;

namespace Ithaca.Application.Decks;

/// <summary>The caller's own gesture on a card: a solo <c>Swipe</c> or a room <c>Vote</c>.</summary>
public sealed class CardGesture
{
    public required Guid DeckItemId { get; init; }

    public required SwipeDirection Direction { get; init; }

    public required SeenVerdict? SeenVerdict { get; init; }
}

/// <summary>
/// Reads deck cards as <see cref="DeckItemDto"/>s for <see cref="DeckService"/> (solo) and <c>RoomDeckService</c> (a room's
/// shared deck); a collaborator, not a use-case service. Each card carries its film, the caller's own gesture and the
/// caller's watchlist flag (false without a user, e.g. a guest).
/// </summary>
public sealed class DeckItemReader(IAppDbContext db)
{
    /// <summary><paramref name="items"/> in position order; <paramref name="gestures"/> are the caller's only.</summary>
    public async Task<IReadOnlyList<DeckItemDto>> ItemDtosAsync(IQueryable<DeckItem> items, IQueryable<CardGesture> gestures, Guid? userId, CancellationToken ct)
    {
        var rows = await (
            from i in items.AsNoTracking()
            join f in db.Films.AsNoTracking() on i.FilmId equals f.Id
            join g in gestures on i.Id equals g.DeckItemId into mine
            from g in mine.DefaultIfEmpty()
            orderby i.Position
            select new
            {
                i.Id,
                i.Position,
                f.TmdbId,
                Direction = g == null ? (SwipeDirection?)null : g.Direction,
                Verdict = g == null ? null : g.SeenVerdict,
                OnWatchlist = userId != null && db.UserFilms.Any(u => u.UserId == userId && u.FilmId == i.FilmId && u.OnWatchlist),
            })
            .ToListAsync(ct);

        var films = await FilmDtosAsync(rows.Select(r => r.TmdbId).ToList(), ct);
        return rows
            .Select(r => new DeckItemDto(
                r.Id, r.Position, films[r.TmdbId], r.Direction is { } direction ? new SwipeDto(direction, r.Verdict) : null, r.OnWatchlist))
            .ToList();
    }

    public async Task<Dictionary<int, FilmDto>> FilmDtosAsync(List<int> tmdbIds, CancellationToken ct) =>
        await db.Films.AsNoTracking()
            .Where(f => tmdbIds.Contains(f.TmdbId))
            .Select(FilmProjections.Dto)
            .ToDictionaryAsync(f => f.TmdbId, ct);
}
