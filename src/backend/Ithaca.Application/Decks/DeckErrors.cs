using Ithaca.Application.Common;
using Ithaca.Domain.Errors;

namespace Ithaca.Application.Decks;

/// <summary>The solo deck errors, defined once (api-contract.md §2).</summary>
public static class DeckErrors
{
    /// <summary>Also for another user's deck: we never confirm it exists.</summary>
    public static readonly Error NotFound = Error.NotFound(ErrorCodes.DeckNotFound, "This deck doesn't exist.");

    public static readonly Error ItemNotFound = Error.NotFound(ErrorCodes.DeckItemNotFound, "This card isn't in this deck.");

    public static readonly Error AlreadySwiped = Error.Conflict(ErrorCodes.DeckAlreadySwiped, "This card has already been swiped.");

    /// <summary>The one verdict rule's error (<c>Swipe.IsValidVerdict</c>), for solo swipes and room votes.</summary>
    public static readonly Error VerdictOnlyWithUp = Error.Validation("seenVerdict", "A verdict only goes with an \"up\" swipe.");

    public static readonly Error NoYeses = new(ErrorCodes.DeckNoYeses, "Swipe right on at least one film first.");
}
