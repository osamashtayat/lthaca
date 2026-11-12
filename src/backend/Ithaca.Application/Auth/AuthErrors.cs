using Ithaca.Application.Common;
using Ithaca.Domain.Errors;

namespace Ithaca.Application.Auth;

/// <summary>The auth errors, defined once (api-contract.md §2).</summary>
public static class AuthErrors
{
    public static readonly Error AntiforgeryFailed = new(ErrorCodes.AuthAntiforgeryFailed, "The request has no valid antiforgery token.");

    public static readonly Error Unauthenticated = new(ErrorCodes.AuthUnauthenticated, "Sign in to continue.");

    /// <summary>Also returned while locked out, so it never reveals which part was wrong.</summary>
    public static readonly Error InvalidCredentials = new(ErrorCodes.AuthInvalidCredentials, "The email or password is wrong.");

    public static readonly Error Forbidden = new(ErrorCodes.AuthForbidden, "You are not allowed to do this.");

    public static readonly Error EmailTaken = Error.Conflict(ErrorCodes.AuthEmailTaken, "That email already has an account.");
}
