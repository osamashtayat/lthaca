using Ithaca.Application.Films;
using Ithaca.Application.Moods;
using Ithaca.Domain.Decks;

namespace Ithaca.Application.Decks;

/// <summary><c>POST /api/decks</c>. <see cref="Mood"/> is required iff <see cref="Source"/> is <c>mood</c> (service rule, 400 <c>errors.mood</c>).</summary>
public sealed record CreateDeckRequest(DeckSource Source, MoodInput? Mood);

/// <summary><c>POST /api/decks/{deckId}/swipes</c>. <see cref="SwipeId"/> makes retries idempotent; a verdict only with "up".</summary>
public sealed record SwipeRequest(
    Guid SwipeId,
    Guid DeckItemId,
    SwipeDirection Direction,
    SeenVerdict? SeenVerdict);

/// <summary>The caller's own swipe (solo) or vote (room) on a card.</summary>
public sealed record SwipeDto(SwipeDirection Direction, SeenVerdict? SeenVerdict);

/// <summary>A card in a solo deck or a room's shared deck.</summary>
public sealed record DeckItemDto(Guid Id, int Position, FilmDto Film, SwipeDto? Swipe, bool OnWatchlist);

/// <summary>A solo deck with every item so far, in position order.</summary>
public sealed record DeckDto(
    Guid Id,
    DeckSource Source,
    DateTimeOffset CreatedAt,
    MoodQueryDto? Mood,
    IReadOnlyList<DeckItemDto> Items,
    int SwipeCount,
    int YesCount);

/// <summary>One of the deck's top picks; <see cref="Reason"/> stays null until BE-13.</summary>
public sealed record TopPickDto(FilmDto Film, string? Reason);

/// <summary><c>GET /api/decks/{deckId}/top</c>: 1–3 picks from the right swipes, best first.</summary>
public sealed record DeckTopDto(int SwipeCount, int YesCount, IReadOnlyList<TopPickDto> Picks);
