using Ithaca.Application.Common;
using Ithaca.Domain.Errors;

namespace Ithaca.Application.Rooms;

/// <summary>The room errors, defined once (api-contract.md §2).</summary>
public static class RoomErrors
{
    public static readonly Error NotFound = Error.NotFound(ErrorCodes.RoomNotFound, "No open or recent room has that code.");

    public static readonly Error NotMember = new(ErrorCodes.RoomNotMember, "You are not a member of this room.");

    public static readonly Error NotHost = new(ErrorCodes.RoomNotHost, "Only the host can do this.");

    public static readonly Error ItemNotFound = Error.NotFound(ErrorCodes.DeckItemNotFound, "This card isn't in this room's deck.");

    public static readonly Error AlreadyVoted = Error.Conflict(ErrorCodes.RoomAlreadyVoted, "You already voted on this card.");

    public static readonly Error Closed = Error.Conflict(ErrorCodes.RoomClosed, "This room is closed.");

    public static readonly Error Full = Error.Conflict(ErrorCodes.RoomFull, "This room is full.");

    public static readonly Error InvalidState = Error.Conflict(ErrorCodes.RoomInvalidState, "The room's current state doesn't allow this.");

    public static readonly Error NoResult = Error.Conflict(ErrorCodes.RoomNoResult, "This room has no result yet.");
}
