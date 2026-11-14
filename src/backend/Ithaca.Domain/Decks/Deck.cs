using Ithaca.Domain.Common;
using Ithaca.Domain.Errors;

namespace Ithaca.Domain.Decks;

/// <summary>
/// A deck (glossary: <c>Deck</c>): a user's solo deck, or a room's shared deck (BE-11); exactly one of <see cref="UserId"/>
/// and <see cref="RoomId"/> is set. How it was started and the <see cref="DeckItem"/>s dealt so far. Cards are dealt in
/// batches of <see cref="BatchSize"/>, up to <see cref="MaxItems"/> (a room's up to <see cref="RoomMaxItems"/>); a film
/// appears at most once per deck.
/// </summary>
public sealed class Deck
{
    /// <summary>Cards per batch, the first one included.</summary>
    public const int BatchSize = 10;

    /// <summary>A deck stops growing here: the next batch is empty.</summary>
    public const int MaxItems = 300;

    /// <summary>A room's shared deck stops growing here (owner, BE-11): the group runs out and the compromise follows (BE-12).</summary>
    public const int RoomMaxItems = 30;

    private Deck(Guid? userId, Guid? roomId, DeckSource source, Guid? moodQueryId, DateTimeOffset now)
    {
        Id = SequentialGuid.NewV7(now);
        UserId = userId;
        RoomId = roomId;
        Source = source;
        MoodQueryId = moodQueryId;
        CreatedAt = now;
    }

    /// <summary>For persistence materialization only.</summary>
    private Deck()
    {
    }

    public Guid Id { get; private set; }

    /// <summary>The owner of a solo deck; null for a room's deck.</summary>
    public Guid? UserId { get; private set; }

    /// <summary>The room of a shared deck (one per room); null for a solo deck.</summary>
    public Guid? RoomId { get; private set; }

    public DeckSource Source { get; private set; }

    /// <summary>The <c>MoodQuery</c> a solo deck by mood was built from; set exactly when a solo deck's <see cref="Source"/> is
    /// mood. A room's moods are its members' (<c>RoomMember.MoodQueryId</c>), so a room deck has none.</summary>
    public Guid? MoodQueryId { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public static Deck Create(Guid userId, DeckSource source, Guid? moodQueryId, DateTimeOffset now)
    {
        if (userId == Guid.Empty)
        {
            throw new DomainException(ErrorCodes.Validation, "userId is required.");
        }

        if ((source == DeckSource.Mood) != (moodQueryId is { } id && id != Guid.Empty))
        {
            throw new DomainException(ErrorCodes.Validation, "A deck by mood needs its mood query, and only it has one.", "mood");
        }

        return new Deck(userId, null, source, moodQueryId, now);
    }

    /// <summary>A room's shared deck, dealt when the host starts (just swipe or mood).</summary>
    public static Deck ForRoom(Guid roomId, DeckSource source, DateTimeOffset now)
    {
        if (roomId == Guid.Empty)
        {
            throw new DomainException(ErrorCodes.Validation, "roomId is required.");
        }

        if (!Enum.IsDefined(source))
        {
            throw new DomainException(ErrorCodes.Validation, "Unknown deck source.", "source");
        }

        return new Deck(null, roomId, source, null, now);
    }
}
