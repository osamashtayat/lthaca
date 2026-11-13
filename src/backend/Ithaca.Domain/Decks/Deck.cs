using Ithaca.Domain.Common;
using Ithaca.Domain.Errors;

namespace Ithaca.Domain.Decks;

/// <summary>
/// A user's solo deck (glossary: <c>Deck</c>): how it was started and the <see cref="DeckItem"/>s dealt so far. Cards are
/// dealt in batches of <see cref="BatchSize"/>, up to <see cref="MaxItems"/>; a film appears at most once per deck.
/// </summary>
public sealed class Deck
{
    /// <summary>Cards per batch, the first one included.</summary>
    public const int BatchSize = 10;

    /// <summary>A deck stops growing here: the next batch is empty.</summary>
    public const int MaxItems = 300;

    private Deck(Guid userId, DeckSource source, Guid? moodQueryId, DateTimeOffset now)
    {
        Id = SequentialGuid.NewV7(now);
        UserId = userId;
        Source = source;
        MoodQueryId = moodQueryId;
        CreatedAt = now;
    }

    /// <summary>For persistence materialization only.</summary>
    private Deck()
    {
    }

    public Guid Id { get; private set; }

    public Guid UserId { get; private set; }

    public DeckSource Source { get; private set; }

    /// <summary>The <c>MoodQuery</c> a deck by mood was built from; set exactly when <see cref="Source"/> is mood.</summary>
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

        return new Deck(userId, source, moodQueryId, now);
    }
}
