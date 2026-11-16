using Ithaca.Application.Auth;
using Sentry;

namespace Ithaca.Api.Monitoring;

/// <summary>
/// Tags this request's Sentry scope with the caller's internal id and kind (user or guest), so an issue shows how many people
/// it hit. Never an email, name or IP (ADR-0028). A no-op when Sentry is off.
/// </summary>
internal sealed class ErrorTrackingUserMiddleware(RequestDelegate next)
{
    public Task InvokeAsync(HttpContext context, ICurrentActor currentActor, IHub hub)
    {
        if (hub.IsEnabled && currentActor.Actor is { } actor)
        {
            hub.ConfigureScope(scope =>
            {
                scope.User = new SentryUser { Id = actor.Id.ToString() };
                scope.SetTag("actor.kind", actor.Kind.ToString().ToLowerInvariant());
            });
        }

        return next(context);
    }
}
