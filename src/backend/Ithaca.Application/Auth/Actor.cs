namespace Ithaca.Application.Auth;

/// <summary>
/// Who is calling, from the server-side session cookie: a <c>User</c>, or a <c>Guest</c> valid for one room
/// (<see cref="RoomId"/>, <see cref="RoomCode"/>; null for users). Services take identity from here, never from request bodies.
/// </summary>
public sealed record Actor(PrincipalKind Kind, Guid Id, Guid? RoomId, string? RoomCode)
{
    public bool IsUser => Kind == PrincipalKind.User;

    public bool IsGuest => Kind == PrincipalKind.Guest;
}

/// <summary>The current request's caller. Implemented by the Api from the cookie principal; services never touch HttpContext.</summary>
public interface ICurrentActor
{
    /// <summary>Null when nobody is signed in.</summary>
    Actor? Actor { get; }
}

/// <summary>
/// The claims a session principal carries beyond the user id (<c>ClaimTypes.NameIdentifier</c>). Shared by whoever writes
/// the principal (Infrastructure) and whoever reads it (the Api); plain strings, no framework types.
/// </summary>
public static class ActorClaims
{
    /// <summary>Present with <see cref="GuestKind"/> on guest principals only; a principal without it is a user.</summary>
    public const string Kind = "ithaca:kind";
    public const string GuestKind = "guest";
    public const string RoomId = "ithaca:room_id";
    public const string RoomCode = "ithaca:room_code";
}
