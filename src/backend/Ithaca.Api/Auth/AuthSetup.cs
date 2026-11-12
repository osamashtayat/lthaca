using Ithaca.Application.Auth;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;

namespace Ithaca.Api.Auth;

/// <summary>
/// How the session cookie behaves, antiforgery, and the authorization policies (ADR-0012, ADR-0023). Identity
/// (Infrastructure) registers the scheme; the Api decides its flags, lifetime and that an API never redirects.
/// </summary>
public static class AuthSetup
{
    public const string SessionCookieName = "ithaca_session";
    public const string AntiforgeryCookieName = "ithaca_af";
    public const string XsrfHeaderName = "X-XSRF-TOKEN";

    /// <summary>Readable by the SPA, which echoes it in <see cref="XsrfHeaderName"/> on every unsafe request.</summary>
    public const string XsrfTokenCookieName = "XSRF-TOKEN";

    /// <summary>Sliding: every visit renews it, so phone users rarely sign in again (owner's choice, ADR-0023).</summary>
    public static readonly TimeSpan SessionLifetime = TimeSpan.FromDays(30);

    public static IServiceCollection AddIthacaAuth(this IServiceCollection services, IHostEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(environment);
        // Secure in Production; Development runs plain HTTP (ADR-0009), where a Secure cookie would never come back.
        var secure = environment.IsProduction() ? CookieSecurePolicy.Always : CookieSecurePolicy.SameAsRequest;

        // PostConfigure: Identity sets the cookie's Events when its own configuration runs.
        services.PostConfigure<CookieAuthenticationOptions>(IdentityConstants.ApplicationScheme, options =>
        {
            options.Cookie.Name = SessionCookieName;
            options.Cookie.HttpOnly = true;
            options.Cookie.SameSite = SameSiteMode.Lax;
            options.Cookie.SecurePolicy = secure;
            options.Cookie.Path = "/";
            options.ExpireTimeSpan = SessionLifetime;
            options.SlidingExpiration = true;
            // An API never redirects to a login page: status-code pages turn these into ProblemDetails.
            options.Events.OnRedirectToLogin = context => SetStatus(context.Response, StatusCodes.Status401Unauthorized);
            options.Events.OnRedirectToAccessDenied = context => SetStatus(context.Response, StatusCodes.Status403Forbidden);
            // Users: Identity re-checks the security stamp periodically. Guests have no Identity account to check against.
            options.Events.OnValidatePrincipal = context =>
                context.Principal.IsGuest()
                    ? Task.CompletedTask
                    : SecurityStampValidator.ValidatePrincipalAsync(context);
        });

        services.AddAntiforgery(options =>
        {
            options.HeaderName = XsrfHeaderName;
            options.Cookie.Name = AntiforgeryCookieName;
            options.Cookie.HttpOnly = true;
            options.Cookie.SameSite = SameSiteMode.Lax;
            options.Cookie.SecurePolicy = secure;
            options.Cookie.Path = "/";
        });

        services.AddAuthorizationBuilder()
            .AddPolicy(AuthPolicies.User, policy => policy
                .RequireAuthenticatedUser()
                .RequireAssertion(context => !context.User.IsGuest()));
        services.AddSingleton<IAuthorizationMiddlewareResultHandler, GuestAwareAuthorizationResultHandler>();

        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentActor, HttpCurrentActor>();
        return services;
    }

    private static Task SetStatus(HttpResponse response, int status)
    {
        response.StatusCode = status;
        return Task.CompletedTask;
    }
}

/// <summary>Authorization policy names, for <c>[Authorize(Policy = …)]</c>.</summary>
public static class AuthPolicies
{
    /// <summary>A signed-in <c>User</c>. Anonymous callers and guests get 401 <c>auth.unauthenticated</c>.</summary>
    public const string User = "user";
}
