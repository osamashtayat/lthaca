using Ithaca.Domain.Decks;
using Ithaca.Domain.Library;

namespace Ithaca.Application.Decks;

/// <summary>
/// How much each thing the user did counts for the next cards (<see cref="DeckSignal.Weight"/>). The deck's own swipes
/// count fully; the library (My Cinema) counts at <see cref="LibraryFactor"/>, since it says less about tonight.
/// </summary>
public static class DeckSignals
{
    public const double LibraryFactor = 0.6;

    /// <summary>A swipe in this deck: right +1, left −1, seen + loved +1.5, seen + not for me −1, seen without a verdict +0.3.</summary>
    public static double OfSwipe(SwipeDirection direction, SeenVerdict? verdict) => (direction, verdict) switch
    {
        (SwipeDirection.Right, _) => 1,
        (SwipeDirection.Left, _) => -1,
        (SwipeDirection.Up, SeenVerdict.Loved) => 1.5,
        (SwipeDirection.Up, SeenVerdict.NotForMe) => -1,
        _ => 0.3,
    };

    /// <summary>
    /// A library row: liked +1, on the watchlist +0.5, a high rating (top fifth of the scale) +1, a low one (bottom two
    /// fifths) −1; summed, then scaled by <see cref="LibraryFactor"/>. Seen alone says nothing about taste: 0.
    /// </summary>
    public static double OfLibrary(bool liked, bool onWatchlist, int? rating)
    {
        double weight = 0;
        weight += liked ? 1 : 0;
        weight += onWatchlist ? 0.5 : 0;
        if (rating is { } r)
        {
            var share = (double)r / UserFilm.MaxRating;
            weight += share >= 0.8 ? 1 : share <= 0.4 ? -1 : 0;
        }

        return weight * LibraryFactor;
    }
}
