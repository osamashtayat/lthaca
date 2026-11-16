using System.ComponentModel.DataAnnotations;
using Ithaca.Application.Common;

namespace Ithaca.Api.Monitoring;

/// <summary>
/// The <c>Sentry</c> section (ADR-0028). Empty <see cref="Dsn"/> = error tracking off (tests, CI, local by default). The DSN
/// comes from the environment (<c>Sentry__Dsn</c>) or user-secrets, never <c>appsettings*.json</c>.
/// </summary>
public sealed class ErrorTrackingOptions : IConfigSection, IValidatableObject
{
    public static string SectionName => "Sentry";

    /// <summary>The project's DSN (<c>ithaca-server</c>); empty or missing turns Sentry off.</summary>
    public string? Dsn { get; init; }

    /// <summary>Share of requests traced. Unset: 1.0 in Development, 0.1 elsewhere.</summary>
    [Range(0.0, 1.0)]
    public double? TracesSampleRate { get; init; }

    /// <summary>The release name shown on issues (the deploy sets it, OPS-07). Unset: the SDK's default from the assembly.</summary>
    public string? Release { get; init; }

    public bool IsEnabled => !string.IsNullOrWhiteSpace(Dsn);

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (IsEnabled && !(Uri.TryCreate(Dsn, UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps && !string.IsNullOrEmpty(uri.UserInfo)))
        {
            yield return new ValidationResult("Sentry:Dsn must be empty or a Sentry DSN (https://<key>@<host>/<project>).", [nameof(Dsn)]);
        }
    }
}
