using System.Globalization;
using IPNetwork = System.Net.IPNetwork;
using System.Security.Claims;
using System.Threading.RateLimiting;
using Ithaca.Api.Errors;
using Ithaca.Domain.Errors;
using Ithaca.Infrastructure.Configuration;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;

namespace Ithaca.Api.RateLimiting;

/// <summary>
/// Request rate limits (BE-19; api-contract.md §2): the policies in <see cref="RateLimitPolicies"/>, applied per action with
/// <c>[EnableRateLimiting]</c>. A request over its limit is refused before any service runs: 429 <c>rate.limited</c> with a
/// <c>Retry-After</c> header (whole seconds). Sign-in/register/password and room join count by client IP always (a session
/// cookie is cheap to drop or renew); the rest count per person (user or guest id), or by IP without a session. The IP is
/// read from <c>X-Forwarded-For</c> only when it comes from a trusted proxy.
/// </summary>
public static partial class RateLimitSetup
{
    public static IServiceCollection AddIthacaRateLimits(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddValidatedOptions<RateLimitOptions>(configuration)
            .Validate(o => o.TrustedProxyNetworks.All(n => IPNetwork.TryParse(n, out _)), "RateLimits:TrustedProxyNetworks: every entry must be a CIDR such as 10.0.0.0/8.")
            .Validate(
                o => RateLimitPolicies.All.All(p => o.RuleFor(p) is { PermitLimit: >= 1 and <= 100_000 } r && r.Window >= TimeSpan.FromSeconds(1) && r.Window <= TimeSpan.FromDays(7)),
                "RateLimits: every rule needs PermitLimit 1–100000 and Window 00:00:01–7.00:00:00.");
        services.AddOptions<ForwardedHeadersOptions>().Configure<IOptions<RateLimitOptions>>((forwarded, limits) =>
        {
            forwarded.ForwardedHeaders = ForwardedHeaders.XForwardedFor;
            foreach (var network in limits.Value.TrustedProxyNetworks)
            {
                forwarded.KnownIPNetworks.Add(IPNetwork.Parse(network));
            }
        });
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = ErrorStatusCodes.For(ErrorCodes.RateLimited);
            options.OnRejected = RejectAsync;
            foreach (var policy in RateLimitPolicies.All)
            {
                options.AddPolicy(policy, context => Partition(context, policy));
            }
        });
        return services;
    }

    private static RateLimitPartition<string> Partition(HttpContext context, string policy)
    {
        var options = context.RequestServices.GetRequiredService<IOptions<RateLimitOptions>>().Value;
        if (!options.Enabled)
        {
            return RateLimitPartition.GetNoLimiter(string.Empty);
        }

        var rule = options.RuleFor(policy);
        var time = context.RequestServices.GetRequiredService<TimeProvider>();
        return RateLimitPartition.Get(PartitionKey(context, policy), _ => new WindowRateLimiter(rule, time));
    }

    /// <summary>
    /// The client's IP for the anonymous policies (auth, room join), so a dropped or fresh guest cookie gives no new count;
    /// otherwise the person (user or guest id) when there's a session, else the IP.
    /// </summary>
    private static string PartitionKey(HttpContext context, string policy)
    {
        var ip = "ip:" + (context.Connection.RemoteIpAddress?.ToString() ?? "unknown");
        if (policy is RateLimitPolicies.Auth or RateLimitPolicies.RoomJoin)
        {
            return ip;
        }

        return context.User.Identity?.IsAuthenticated == true && context.User.FindFirstValue(ClaimTypes.NameIdentifier) is { } id
            ? "id:" + id
            : ip;
    }

    private static async ValueTask RejectAsync(OnRejectedContext rejected, CancellationToken ct)
    {
        var context = rejected.HttpContext;
        var seconds = rejected.Lease.TryGetMetadata(MetadataName.RetryAfter, out var wait)
            ? Math.Max(1, (int)Math.Ceiling(wait.TotalSeconds))
            : 1;
        var status = ErrorStatusCodes.For(ErrorCodes.RateLimited);
        context.Response.StatusCode = status;
        context.Response.Headers.RetryAfter = seconds.ToString(CultureInfo.InvariantCulture);
        var logger = context.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger(typeof(RateLimitSetup));
        LogRejected(logger, context.GetEndpoint()?.DisplayName ?? context.Request.Path.Value ?? "", seconds);
        var problem = new ProblemDetails
        {
            Status = status,
            Detail = "Too many requests. Try again later.",
            Extensions = { [ProblemDetailsSetup.CodeKey] = ErrorCodes.RateLimited },
        };
        await context.RequestServices.GetRequiredService<IProblemDetailsService>().WriteAsync(new ProblemDetailsContext
        {
            HttpContext = context,
            ProblemDetails = problem,
        });
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Rate limited {Endpoint}; retry after {Seconds} s")]
    private static partial void LogRejected(ILogger logger, string endpoint, int seconds);
}
