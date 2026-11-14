namespace Ithaca.Domain.Decks;

/// <summary>Right = I'd watch it, left = not for me, up = seen it (glossary: Vote). Used by solo swipes and room votes.</summary>
public enum SwipeDirection
{
    Right,
    Left,
    Up,
}

/// <summary>The optional rating after an "up" swipe; null means the person skipped it.</summary>
public enum SeenVerdict
{
    Loved,
    NotForMe,
}

/// <summary>How a solo deck was started. "taste" is in the contract since BE-14a; decks by taste are built by BE-14.</summary>
public enum DeckSource
{
    JustSwipe,
    Mood,
    Taste,
}
