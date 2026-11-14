namespace Ithaca.Domain.Rooms;

/// <summary>How a room's shared deck is built. "taste" (group taste) is in the contract since BE-14a; BE-14 builds it.</summary>
public enum RoomMode
{
    JustSwipe,
    Mood,
    Taste,
}

/// <summary>A room's lifecycle (docs/realtime.md).</summary>
public enum RoomStatus
{
    Lobby,
    Swiping,
    Result,
    Closed,
}
