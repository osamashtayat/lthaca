using Ithaca.Domain.Decks;
using Ithaca.Domain.Errors;

namespace Ithaca.Domain.Rooms;

/// <summary>
/// A room member's swipe on a card of the room's shared deck (glossary: <c>Vote</c>; ADR-0015). One per
/// (<see cref="DeckItemId"/>, <see cref="RoomMemberId"/>) and never changed (A5). Its id is the client's <c>voteId</c>, so a
/// retried request finds it and is a no-op. A <see cref="SeenVerdict"/> only goes with "up" and never touches the library.
/// </summary>
public sealed class Vote
{
    private Vote(Guid id, Guid deckItemId, Guid roomMemberId, SwipeDirection direction, SeenVerdict? seenVerdict, DateTimeOffset now)
    {
        Id = id;
        DeckItemId = deckItemId;
        RoomMemberId = roomMemberId;
        Direction = direction;
        SeenVerdict = seenVerdict;
        CreatedAt = now;
    }

    /// <summary>For persistence materialization only.</summary>
    private Vote()
    {
    }

    public Guid Id { get; private set; }

    public Guid DeckItemId { get; private set; }

    public Guid RoomMemberId { get; private set; }

    public SwipeDirection Direction { get; private set; }

    public SeenVerdict? SeenVerdict { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public static Vote Create(Guid voteId, Guid deckItemId, Guid roomMemberId, SwipeDirection direction, SeenVerdict? seenVerdict, DateTimeOffset now)
    {
        if (voteId == Guid.Empty || deckItemId == Guid.Empty || roomMemberId == Guid.Empty)
        {
            throw new DomainException(ErrorCodes.Validation, "voteId, deckItemId and roomMemberId are required.");
        }

        if (!Enum.IsDefined(direction))
        {
            throw new DomainException(ErrorCodes.Validation, "Unknown direction.", "direction");
        }

        if (!Swipe.IsValidVerdict(direction, seenVerdict))
        {
            throw new DomainException(ErrorCodes.Validation, "seenVerdict only goes with an \"up\" vote.", "seenVerdict");
        }

        return new Vote(voteId, deckItemId, roomMemberId, direction, seenVerdict, now);
    }
}
