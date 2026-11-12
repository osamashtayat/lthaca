using Ithaca.Api.Errors;
using Ithaca.Application.Auth;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.AspNetCore.Mvc.ApplicationModels;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Ithaca.Api.Auth;

/// <summary>
/// Global filter: every unsafe request needs a valid <c>X-XSRF-TOKEN</c> header matching the antiforgery cookie. No
/// endpoint is exempt, sign-in included: the client gets its first token from <c>GET /api/auth/me</c> (api-contract.md §4).
/// Failure → 400 <c>auth.antiforgery_failed</c>.
/// </summary>
public sealed class ValidateAntiforgeryFilter(IAntiforgery antiforgery) : IAsyncAuthorizationFilter
{
    public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var http = context.HttpContext;
        if (!AntiforgeryRules.IsUnsafe(http.Request.Method) || await antiforgery.IsRequestValidAsync(http))
        {
            return;
        }

        context.Result = http.ToProblemResult(AuthErrors.AntiforgeryFailed);
    }
}

/// <summary>
/// Issues the readable <c>XSRF-TOKEN</c> cookie after the action ran, success or error (so <c>/me</c> issues one even
/// when it answers 401). Put it on every action that can change the caller: the token is bound to
/// <c>HttpContext.User</c>, which sign-in, sign-out and register have already switched.
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class IssueXsrfTokenAttribute : Attribute, IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(next);
        var executed = await next();
        if (executed.Exception is not null && !executed.ExceptionHandled)
        {
            return;
        }

        var http = context.HttpContext;
        var tokens = http.RequestServices.GetRequiredService<IAntiforgery>().GetAndStoreTokens(http);
        var secure = http.RequestServices.GetRequiredService<XsrfCookieOptions>().SecurePolicy;
        http.Response.Cookies.Append(AuthSetup.XsrfTokenCookieName, tokens.RequestToken!, new CookieOptions
        {
            HttpOnly = false,
            SameSite = SameSiteMode.Lax,
            Secure = secure == CookieSecurePolicy.Always || (secure == CookieSecurePolicy.SameAsRequest && http.Request.IsHttps),
            Path = "/",
        });
    }
}

/// <summary>Documents the antiforgery 400 on every unsafe action that doesn't already declare a 400.</summary>
public sealed class AntiforgeryResponseConvention : IActionModelConvention
{
    public void Apply(ActionModel action)
    {
        ArgumentNullException.ThrowIfNull(action);
        var unsafeAction = action.Selectors
            .SelectMany(s => s.ActionConstraints?.OfType<Microsoft.AspNetCore.Mvc.ActionConstraints.HttpMethodActionConstraint>() ?? [])
            .SelectMany(c => c.HttpMethods)
            .Any(AntiforgeryRules.IsUnsafe);
        var declares400 = action.Filters.OfType<IApiResponseMetadataProvider>().Any(p => p.StatusCode == StatusCodes.Status400BadRequest);
        if (unsafeAction && !declares400)
        {
            action.Filters.Add(new ProducesResponseTypeAttribute(typeof(ProblemDetails), StatusCodes.Status400BadRequest));
        }
    }
}

internal static class AntiforgeryRules
{
    public static bool IsUnsafe(string method) =>
        !(HttpMethods.IsGet(method) || HttpMethods.IsHead(method) || HttpMethods.IsOptions(method) || HttpMethods.IsTrace(method));
}
