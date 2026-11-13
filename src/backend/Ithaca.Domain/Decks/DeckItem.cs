using Ithaca.Domain.Common;
using Ithaca.Domain.Errors;

namespace Ithaca.Domain.Decks;

/// <summary>
/// One card (a film) at a position in a <see cref="Deck"/> (glossary: <c>DeckItem</c>). Positions start at 1 and increase in
/// the order cards are dealt. Its film can't be purged while it exists (ADR-0019).
/// </summary>
public sealed class DeckItem
{
    private DeckItem(Guid deckId, Guid filmId, int position, DateTimeOffset now)
    {
        Id = SequentialGuid.NewV7(now);
        DeckId = deckId;
        FilmId = filmId;
        Position = position;
        CreatedAt = now;
    }

    /// <summary>For persistence materialization only.</summary>
    private DeckItem()
    {
    }

    public Guid Id { get; private set; }

    public Guid DeckId { get; private set; }

    public Guid FilmId { get; private set; }

    public int Position { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public static DeckItem Create(Guid deckId, Guid filmId, int position, DateTimeOffset now)
    {
        if (deckId == Guid.Empty || filmId == Guid.Empty)
        {
            throw new DomainException(ErrorCodes.Validation, "deckId and filmId are required.");
        }

        if (position is < 1 or > Deck.MaxItems)
        {
            throw new DomainException(ErrorCodes.Validation, $"position must be 1-{Deck.MaxItems}.", "position");
        }

        return new DeckItem(deckId, filmId, position, now);
    }
}
