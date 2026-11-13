using System.Security.Claims;
using Ithaca.Application.Auth;

namespace Ithaca.Api.Auth;

/// <summary>The one "who is this principal" rule for the Api's auth plumbing (services use <c>ICurrentActor</c>).</summary>
internal static class ClaimsPrincipalExtensions
{
    /// <summary>A guest principal (room-scoped, no Identity account). Anything else that is authenticated is a user.</summary>
    public static bool IsGuest(this ClaimsPrincipal? principal) =>
        principal?.HasClaim(ActorClaims.Kind, ActorClaims.GuestKind) == true;
}
