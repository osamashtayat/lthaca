using Ithaca.Domain.Errors;

namespace Ithaca.Domain.Decks;

/// <summary>
/// A user's gesture on a card in their solo deck (glossary: <c>Swipe</c>; a room member's is a <c>Vote</c>). One per
/// <see cref="DeckItem"/>. Its id is the client's <c>swipeId</c>, so a retried request finds it and is a no-op.
/// A <see cref="SeenVerdict"/> only goes with <see cref="SwipeDirection.Up"/>.
/// </summary>
public sealed class Swipe
{
    private Swipe(Guid id, Guid deckItemId, SwipeDirection direction, SeenVerdict? seenVerdict, DateTimeOffset now)
    {
        Id = id;
        DeckItemId = deckItemId;
        Direction = direction;
        SeenVerdict = seenVerdict;
        CreatedAt = now;
    }

    /// <summary>For persistence materialization only.</summary>
    private Swipe()
    {
    }

    public Guid Id { get; private set; }

    public Guid DeckItemId { get; private set; }

    public SwipeDirection Direction { get; private set; }

    public SeenVerdict? SeenVerdict { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>The one verdict rule, shared with room votes: a verdict only with "up".</summary>
    public static bool IsValidVerdict(SwipeDirection direction, SeenVerdict? seenVerdict) =>
        seenVerdict is null || direction == SwipeDirection.Up;

    public static Swipe Create(Guid swipeId, Guid deckItemId, SwipeDirection direction, SeenVerdict? seenVerdict, DateTimeOffset now)
    {
        if (swipeId == Guid.Empty || deckItemId == Guid.Empty)
        {
            throw new DomainException(ErrorCodes.Validation, "swipeId and deckItemId are required.");
        }

        if (!IsValidVerdict(direction, seenVerdict))
        {
            throw new DomainException(ErrorCodes.Validation, "seenVerdict only goes with an \"up\" swipe.", "seenVerdict");
        }

        return new Swipe(swipeId, deckItemId, direction, seenVerdict, now);
    }
}
