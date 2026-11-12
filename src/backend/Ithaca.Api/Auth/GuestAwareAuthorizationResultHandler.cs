using Ithaca.Api.Errors;
using Ithaca.Application.Auth;
using Ithaca.Domain.Errors;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;

namespace Ithaca.Api.Auth;

/// <summary>
/// A guest calling a user-only endpoint is "not signed in as a user": 401 <c>auth.unauthenticated</c>, not 403
/// (api-contract.md §2). Everything else keeps the default (challenge → 401, forbid → 403); the cookie events and
/// status-code pages make those ProblemDetails.
/// </summary>
public sealed class GuestAwareAuthorizationResultHandler : IAuthorizationMiddlewareResultHandler
{
    private readonly AuthorizationMiddlewareResultHandler _default = new();

    public Task HandleAsync(RequestDelegate next, HttpContext context, AuthorizationPolicy policy, PolicyAuthorizationResult authorizeResult)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(authorizeResult);
        if (authorizeResult.Forbidden && context.User.HasClaim(ActorClaims.Kind, ActorClaims.GuestKind))
        {
            context.Items[ProblemDetailsSetup.CodeItemKey] = ErrorCodes.AuthUnauthenticated;
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return Task.CompletedTask;
        }

        return _default.HandleAsync(next, context, policy, authorizeResult);
    }
}
