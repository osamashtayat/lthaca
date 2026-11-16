namespace Ithaca.Application.Realtime;

/// <summary>
/// Server → client event names on the room hub. Source of truth: <c>contracts/realtime-events.json</c>, which the
/// frontend imports directly; a test fails if these constants and that file differ. Add an event in BOTH (docs/realtime.md).
/// In Application (ADR-0025) because the services that publish after their commit choose the event.
/// </summary>
public static class RoomEvents
{
    public const string HubPath = "/hubs/rooms";

    public const string MemberJoined = "memberJoined";
    public const string MemberLeft = "memberLeft";
    public const string MemberReady = "memberReady";
    public const string RoomStarted = "roomStarted";
    public const string VoteCast = "voteCast";
    public const string DeckExtended = "deckExtended";
    public const string RoomMatched = "roomMatched";
    public const string RoomClosed = "roomClosed";

    /// <summary>The SignalR group holding every connection of one room.</summary>
    public static string GroupFor(Guid roomId) => $"room:{roomId:N}";
}

/// <summary><c>memberJoined</c> / <c>memberLeft</c> / <c>memberReady</c>.</summary>
public sealed record RoomMemberEvent(Guid RoomId, Guid MemberId);

/// <summary><c>roomStarted</c> / <c>roomClosed</c>.</summary>
public sealed record RoomEvent(Guid RoomId);

/// <summary><c>voteCast</c>: which card got a vote, never its value (ADR-0015).</summary>
public sealed record VoteCastEvent(Guid RoomId, Guid DeckItemId);

/// <summary><c>deckExtended</c>: the first position of the new batch.</summary>
public sealed record DeckExtendedEvent(Guid RoomId, int FromPosition);

/// <summary><c>roomMatched</c>: the matched film, or the compromise's #1 (null when nobody said yes to anything).</summary>
public sealed record RoomMatchedEvent(Guid RoomId, int? TmdbId);
