namespace Ithaca.Application.Decks;

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

/// <summary>How a solo deck was started. "taste" is added by the taste task (additive).</summary>
public enum DeckSource
{
    JustSwipe,
    Mood,
}
