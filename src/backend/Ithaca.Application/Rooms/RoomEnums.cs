namespace Ithaca.Application.Rooms;

/// <summary>How a room's shared deck is built. "taste" (group taste) is added by the taste task (additive).</summary>
public enum RoomMode
{
    JustSwipe,
    Mood,
}

/// <summary>A room's lifecycle (docs/realtime.md).</summary>
public enum RoomStatus
{
    Lobby,
    Swiping,
    Result,
    Closed,
}

/// <summary>Everyone said yes to one film, or the best compromise was ranked.</summary>
public enum RoomResultKind
{
    Match,
    Compromise,
}
