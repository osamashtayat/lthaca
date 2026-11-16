using Microsoft.AspNetCore.SignalR;

namespace Ithaca.Api.Hubs;

/// <summary>
/// Logs an unexpected exception from a hub method at Error, so it reaches the logs and Sentry like a failed request does
/// (SignalR itself logs it only at Debug). The client still gets SignalR's generic error; nothing is swallowed. Expected
/// failures are <c>Result</c> errors returned by the services, not exceptions.
/// </summary>
public sealed partial class HubErrorLoggingFilter(ILogger<HubErrorLoggingFilter> logger) : IHubFilter
{
    public async ValueTask<object?> InvokeMethodAsync(HubInvocationContext invocationContext, Func<HubInvocationContext, ValueTask<object?>> next)
    {
        ArgumentNullException.ThrowIfNull(invocationContext);
        ArgumentNullException.ThrowIfNull(next);
        try
        {
            return await next(invocationContext);
        }
        catch (Exception e) when (e is not HubException and not OperationCanceledException)
        {
            LogFailed(logger, e, invocationContext.HubMethodName);
            throw;
        }
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Hub method {Method} failed")]
    private static partial void LogFailed(ILogger logger, Exception exception, string method);
}
