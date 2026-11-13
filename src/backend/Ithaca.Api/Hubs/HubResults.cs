using Ithaca.Application.Common;
using Microsoft.AspNetCore.SignalR;

namespace Ithaca.Api.Hubs;

/// <summary>The ONLY place a <see cref="Result"/> becomes a hub answer, as <c>ResultExtensions</c> is for HTTP.</summary>
internal static class HubResults
{
    /// <summary>The value, or a <see cref="HubException"/> whose message is the error code (e.g. <c>room.not_member</c>).</summary>
    public static T OrThrowHubException<T>(this Result<T> result)
    {
        ArgumentNullException.ThrowIfNull(result);
        return result.IsSuccess ? result.Value : throw new HubException(result.Error.Code);
    }
}
