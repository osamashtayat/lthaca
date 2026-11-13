using System.Security.Claims;
using Ithaca.Application.Auth;

namespace Ithaca.Api.Auth;

/// <summary>
/// <see cref="ICurrentActor"/> from the authenticated cookie principal: the only place claims are read for services. In a
/// hub method it is the connection's principal (<see cref="HubCallerPrincipal"/>), otherwise the HTTP request's.
/// </summary>
public sealed class HttpCurrentActor(IHttpContextAccessor accessor, HubCallerPrincipal hubCaller) : ICurrentActor
{
    public Actor? Actor
    {
        get
        {
            var principal = hubCaller.Principal ?? accessor.HttpContext?.User;
            if (principal?.Identity?.IsAuthenticated != true || !Guid.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var id))
            {
                return null;
            }

            if (!principal.IsGuest())
            {
                return new Actor(PrincipalKind.User, id, null, null);
            }

            return Guid.TryParse(principal.FindFirstValue(ActorClaims.RoomId), out var roomId)
                ? new Actor(PrincipalKind.Guest, id, roomId, principal.FindFirstValue(ActorClaims.RoomCode))
                : null;
        }
    }
}
