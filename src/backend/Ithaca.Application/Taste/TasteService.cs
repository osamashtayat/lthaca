using Ithaca.Application.Auth;
using Ithaca.Application.Common;
using Ithaca.Domain.Decks;
using Ithaca.Domain.Taste;
using Microsoft.EntityFrameworkCore;

namespace Ithaca.Application.Taste;

/// <summary>
/// The caller's taste (api-contract.md §12, BE-14): readiness for By taste and the taste receipt. Users only (guests have no
/// library, ADR-0012). The server decides readiness; the screen only shows it.
/// </summary>
public sealed class TasteService(IAppDbContext db, ICurrentActor current, TasteProfileReader profiles)
{
    /// <summary>Most recent solo swipes read for <c>notForYou</c>.</summary>
    private const int SwipeLimit = 500;

    public async Task<Result<TasteDto>> GetAsync(CancellationToken ct)
    {
        if (current.Actor is not { IsUser: true } actor)
        {
            return AuthErrors.Unauthenticated;
        }

        var userId = actor.Id;
        var signalCount = await profiles.SignalCountAsync(userId, ct);
        var loggedCount = await db.UserFilms.CountAsync(u => u.UserId == userId && (u.Seen || u.RatingHalfStars != null), ct);
        var ready = signalCount >= profiles.SignalsNeeded;
        if (!ready)
        {
            return new TasteDto(false, signalCount, profiles.SignalsNeeded, loggedCount, [], []);
        }

        var library = db.UserFilms.AsNoTracking().Where(u => u.UserId == userId);
        var liked = await library
            .Where(u => u.Liked || u.RatingHalfStars >= TasteProfile.PositiveRatingHalfStars)
            .Join(db.Films, u => u.FilmId, f => f.Id, (u, f) => f.Genres)
            .ToListAsync(ct);
        var lowRated = await library
            .Where(u => !u.Liked && u.RatingHalfStars <= TasteProfile.LowRatingHalfStars)
            .Join(db.Films, u => u.FilmId, f => f.Id, (u, f) => f.Genres)
            .ToListAsync(ct);
        var swipedDown = await (
                from d in db.Decks.AsNoTracking()
                where d.UserId == userId
                join i in db.DeckItems.AsNoTracking() on d.Id equals i.DeckId
                join s in db.Swipes.AsNoTracking() on i.Id equals s.DeckItemId
                where s.Direction == SwipeDirection.Left || (s.Direction == SwipeDirection.Up && s.SeenVerdict == SeenVerdict.NotForMe)
                join f in db.Films.AsNoTracking() on i.FilmId equals f.Id
                orderby s.CreatedAt descending
                select new { f.Id, f.Genres })
            .Take(SwipeLimit)
            .ToListAsync(ct);

        IReadOnlyList<IReadOnlyList<string>> likedGenres = liked;
        var turnedDown = lowRated.Concat(swipedDown.DistinctBy(s => s.Id).Select(s => s.Genres)).ToList<IReadOnlyList<string>>();
        var top = TasteReceipt.TopGenres(likedGenres);
        return new TasteDto(true, signalCount, profiles.SignalsNeeded, loggedCount, top, TasteReceipt.NotForYou(likedGenres, turnedDown, top));
    }
}
