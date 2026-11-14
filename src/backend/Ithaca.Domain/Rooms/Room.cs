using Ithaca.Domain.Common;
using Ithaca.Domain.Errors;

namespace Ithaca.Domain.Rooms;

/// <summary>
/// A group session joined by link or code (glossary: <c>Room</c>). Its people are <see cref="RoomMember"/>s, at most
/// <see cref="MaxMembers"/> at a time. The host isn't stored: it is the member who has been in the room longest
/// (earliest <see cref="RoomMember.JoinedAt"/>, then id), so the creator hosts while present and hosting passes on when they leave.
/// </summary>
public sealed class Room
{
    public const int NameMaxLength = 40;

    /// <summary>"ADMIT 2–8".</summary>
    public const int MaxMembers = 8;

    private Room(string code, string name, RoomMode mode, DateTimeOffset? startsAt, DateTimeOffset now)
    {
        Id = SequentialGuid.NewV7(now);
        Code = code;
        Name = name;
        Mode = mode;
        Status = RoomStatus.Lobby;
        StartsAt = startsAt;
        CreatedAt = now;
    }

    /// <summary>For persistence materialization only.</summary>
    private Room()
    {
        Code = "";
        Name = "";
    }

    public Guid Id { get; private set; }

    /// <summary>Upper case, unique (<see cref="RoomCode"/>).</summary>
    public string Code { get; private set; }

    public string Name { get; private set; }

    public RoomMode Mode { get; private set; }

    public RoomStatus Status { get; private set; }

    public DateTimeOffset? StartsAt { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset? ClosedAt { get; private set; }

    /// <summary>New members may join in the lobby and while swiping ("late arrivals can still hop in"), not once finished or closed.</summary>
    public bool AcceptsJoins => AcceptsJoinsIn(Status);

    /// <summary>The join rule for a status alone (for reads that project the status instead of loading the room).</summary>
    public static bool AcceptsJoinsIn(RoomStatus status) => status is RoomStatus.Lobby or RoomStatus.Swiping;

    public static Room Create(string code, string name, RoomMode mode, DateTimeOffset? startsAt, DateTimeOffset now)
    {
        if (RoomCode.Normalize(code) != code)
        {
            throw new DomainException(ErrorCodes.Validation, "The room code is malformed.", "code");
        }

        var trimmed = name?.Trim();
        if (string.IsNullOrEmpty(trimmed) || trimmed.Length > NameMaxLength)
        {
            throw new DomainException(ErrorCodes.Validation, $"name must be 1-{NameMaxLength} characters.", "name");
        }

        if (!Enum.IsDefined(mode))
        {
            throw new DomainException(ErrorCodes.Validation, "Unknown room mode.", "mode");
        }

        return new Room(code, trimmed, mode, startsAt, now);
    }

    /// <summary>Ends the room for everyone. False when it was already closed (nothing changed, nothing to announce).</summary>
    public bool Close(DateTimeOffset now)
    {
        if (Status == RoomStatus.Closed)
        {
            return false;
        }

        Status = RoomStatus.Closed;
        ClosedAt = now;
        return true;
    }
}
