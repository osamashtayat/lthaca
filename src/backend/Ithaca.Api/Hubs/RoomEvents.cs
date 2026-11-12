namespace Ithaca.Api.Hubs;

/// <summary>
/// Server → client event names on <see cref="RoomHub"/>. Source of truth: <c>contracts/realtime-events.json</c>, which the
/// frontend imports directly; a test fails if these constants and that file differ. Add an event in BOTH (docs/realtime.md).
/// </summary>
public static class RoomEvents
{
    public const string HubPath = "/hubs/rooms";

    public const string MemberJoined = "memberJoined";
    public const string MemberLeft = "memberLeft";
    public const string VoteCast = "voteCast";
    public const string DeckExtended = "deckExtended";
    public const string RoomMatched = "roomMatched";
    public const string RoomClosed = "roomClosed";

    /// <summary>The SignalR group holding every connection of one room.</summary>
    public static string GroupFor(Guid roomId) => $"room:{roomId:N}";
}
