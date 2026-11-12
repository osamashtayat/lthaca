using System.Security.Claims;
using Ithaca.Application.Auth;

namespace Ithaca.Api.Auth;

/// <summary><see cref="ICurrentActor"/> from the authenticated cookie principal: the only place claims are read for services.</summary>
public sealed class HttpCurrentActor(IHttpContextAccessor accessor) : ICurrentActor
{
    public Actor? Actor
    {
        get
        {
            var principal = accessor.HttpContext?.User;
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
