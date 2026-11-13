using Ithaca.Domain.Common;
using Ithaca.Domain.Errors;

namespace Ithaca.Domain.Rooms;

/// <summary>
/// A <c>User</c> or a <c>Guest</c> in a <see cref="Room"/> (glossary: <c>RoomMember</c>); exactly one of
/// <see cref="UserId"/> and <see cref="GuestId"/> is set. Leaving keeps the row (<see cref="LeftAt"/>), so a user who comes
/// back is the same member. A present member holds a <see cref="Seat"/> (1–<see cref="Room.MaxMembers"/>), unique among the
/// room's present members, which is what keeps a room at most full even when people join at the same moment.
/// </summary>
public sealed class RoomMember
{
    private RoomMember(Guid roomId, Guid? userId, Guid? guestId, int seat, DateTimeOffset now)
    {
        Id = SequentialGuid.NewV7(now);
        RoomId = roomId;
        UserId = userId;
        GuestId = guestId;
        Seat = seat;
        JoinedAt = now;
    }

    /// <summary>For persistence materialization only.</summary>
    private RoomMember()
    {
    }

    public Guid Id { get; private set; }

    public Guid RoomId { get; private set; }

    public Guid? UserId { get; private set; }

    public Guid? GuestId { get; private set; }

    public int Seat { get; private set; }

    /// <summary>When this member (last) joined. Coming back after leaving starts again from then.</summary>
    public DateTimeOffset JoinedAt { get; private set; }

    /// <summary>Null while in the room.</summary>
    public DateTimeOffset? LeftAt { get; private set; }

    public bool IsPresent => LeftAt is null;

    public static RoomMember ForUser(Guid roomId, Guid userId, int seat, DateTimeOffset now) =>
        userId == Guid.Empty ? throw new DomainException(ErrorCodes.Validation, "userId is required.") : Create(roomId, userId, null, seat, now);

    public static RoomMember ForGuest(Guid roomId, Guid guestId, int seat, DateTimeOffset now) =>
        guestId == Guid.Empty ? throw new DomainException(ErrorCodes.Validation, "guestId is required.") : Create(roomId, null, guestId, seat, now);

    public void Leave(DateTimeOffset now) => LeftAt ??= now;

    /// <summary>A user who left comes back to the same membership, in a free seat.</summary>
    public void Rejoin(int seat, DateTimeOffset now)
    {
        EnsureSeat(seat);
        if (IsPresent)
        {
            return;
        }

        Seat = seat;
        JoinedAt = now;
        LeftAt = null;
    }

    /// <summary>
    /// Guest upgrade (ADR-0012): the guest signed up or signed in, so this membership (and later its votes) becomes the user's.
    /// </summary>
    public void TransferToUser(Guid userId)
    {
        if (GuestId is null || userId == Guid.Empty)
        {
            throw new DomainException(ErrorCodes.Validation, "Only a guest's membership moves to a user.");
        }

        GuestId = null;
        UserId = userId;
    }

    private static RoomMember Create(Guid roomId, Guid? userId, Guid? guestId, int seat, DateTimeOffset now)
    {
        if (roomId == Guid.Empty)
        {
            throw new DomainException(ErrorCodes.Validation, "roomId is required.");
        }

        EnsureSeat(seat);
        return new RoomMember(roomId, userId, guestId, seat, now);
    }

    private static void EnsureSeat(int seat)
    {
        if (seat is < 1 or > Room.MaxMembers)
        {
            throw new DomainException(ErrorCodes.Validation, $"seat must be 1-{Room.MaxMembers}.", "seat");
        }
    }
}
