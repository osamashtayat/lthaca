namespace Ithaca.Domain.Rooms;

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
