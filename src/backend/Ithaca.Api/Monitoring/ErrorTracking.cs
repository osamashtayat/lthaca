using Ithaca.Infrastructure.Configuration;
using Microsoft.Extensions.Options;
using Sentry;
using Sentry.AspNetCore;
using Sentry.Extensibility;

namespace Ithaca.Api.Monitoring;

/// <summary>
/// Sentry for the API (ADR-0028): unhandled exceptions and every Error log (requests, background jobs, the room hub) become
/// issues; Warning-and-above logs go to Sentry Logs, so handled failures (AI, TMDB, Voyage, Resend) are searchable; requests
/// are traced. Off without a DSN. Users are reported by internal id only (<see cref="ErrorTrackingUserMiddleware"/>).
/// </summary>
public static class ErrorTracking
{
    /// <summary>Call after the logging providers are set up, so the Sentry provider is added to them.</summary>
    public static WebApplicationBuilder AddIthacaErrorTracking(this WebApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.Services.AddValidatedOptions<ErrorTrackingOptions>(builder.Configuration);
        builder.WebHost.UseSentry();

        // Registered after UseSentry's own binding of the section, so these values win.
        builder.Services.AddOptions<SentryAspNetCoreOptions>()
            .Configure<IOptions<ErrorTrackingOptions>, IHostEnvironment>((sentry, ours, environment) => Configure(sentry, ours.Value, environment));
        return builder;
    }

    /// <summary>Adds the internal user id to each request's events; after authentication.</summary>
    public static IApplicationBuilder UseIthacaErrorTracking(this IApplicationBuilder app) =>
        app.UseMiddleware<ErrorTrackingUserMiddleware>();

    /// <summary>
    /// Sentry Logs get Warning and above, except EF Core's: it logs every handled write conflict at Error (also filtered for
    /// issues by <c>Logging:Sentry</c> in appsettings.json).
    /// </summary>
    internal static bool ShouldSendLog(SentryLogLevel level, string? category) =>
        level >= SentryLogLevel.Warning && !(category?.StartsWith("Microsoft.EntityFrameworkCore", StringComparison.Ordinal) ?? false);

    internal static void Configure(SentryAspNetCoreOptions sentry, ErrorTrackingOptions options, IHostEnvironment environment)
    {
        // Empty string, not null: null would make the SDK fall back to a SENTRY_DSN environment variable.
        sentry.Dsn = options.IsEnabled ? options.Dsn : string.Empty;
        sentry.Environment = environment.EnvironmentName.ToLowerInvariant();
        if (!string.IsNullOrWhiteSpace(options.Release))
        {
            sentry.Release = options.Release;
        }

        sentry.SendDefaultPii = false;
        sentry.MaxRequestBodySize = RequestSize.None;
        sentry.TracesSampleRate = options.TracesSampleRate ?? (environment.IsDevelopment() ? 1.0 : 0.1);
        sentry.MinimumEventLevel = LogLevel.Error;
        sentry.MinimumBreadcrumbLevel = LogLevel.Information;
        sentry.EnableLogs = true;
        sentry.SetBeforeSendLog(log => ShouldSendLog(log.Level, log.TryGetAttribute("category.name", out var category) ? category as string : null) ? log : null);
        sentry.SetBeforeSend(sentryEvent => SentryScrubber.Scrub(sentryEvent));
        sentry.SetBeforeSendTransaction(transaction => SentryScrubber.Scrub(transaction));
        sentry.SetBeforeBreadcrumb(breadcrumb => SentryScrubber.Scrub(breadcrumb));
    }
}
