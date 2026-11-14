using System.Security.Claims;
using Microsoft.AspNetCore.SignalR;

namespace Ithaca.Api.Auth;

/// <summary>
/// The principal of the hub connection whose method is running in this scope. A hub invocation isn't an HTTP request
/// (with long polling the connecting request is long gone), so <see cref="HttpCurrentActor"/> reads this first.
/// </summary>
public sealed class HubCallerPrincipal
{
    public ClaimsPrincipal? Principal { get; set; }
}

/// <summary>Sets <see cref="HubCallerPrincipal"/> for every hub method call, before the hub's services read the actor.</summary>
public sealed class HubCallerPrincipalFilter : IHubFilter
{
    public ValueTask<object?> InvokeMethodAsync(HubInvocationContext invocationContext, Func<HubInvocationContext, ValueTask<object?>> next)
    {
        ArgumentNullException.ThrowIfNull(invocationContext);
        ArgumentNullException.ThrowIfNull(next);
        invocationContext.ServiceProvider.GetRequiredService<HubCallerPrincipal>().Principal = invocationContext.Context.User;
        return next(invocationContext);
    }
}
