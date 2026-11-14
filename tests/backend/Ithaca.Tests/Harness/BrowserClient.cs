using System.Net;
using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Mvc.Testing.Handlers;

namespace Ithaca.Tests.Harness;

/// <summary>
/// An HttpClient that behaves like the SPA (api-contract.md §4): keeps cookies, remembers the latest <c>XSRF-TOKEN</c>
/// cookie and echoes it in <c>X-XSRF-TOKEN</c> on every unsafe request. <see cref="StartAsync"/> fetches the first token
/// from <c>GET /api/auth/me</c>, as the SPA does on load.
/// </summary>
internal static class BrowserClient
{
    private static readonly ConditionalWeakTable<HttpClient, CookieContainer> Cookies = new();

    /// <param name="baseAddress">The server's address; defaults to the in-memory TestServer's (a Kestrel-hosted factory passes its own).</param>
    public static async Task<HttpClient> StartAsync(WebApplicationFactory<Program> factory, CancellationToken ct, Uri? baseAddress = null)
    {
        var cookies = new CookieContainer();
        var client = factory.CreateDefaultClient(baseAddress ?? new Uri("http://localhost"), new XsrfHandler(), new CookieContainerHandler(cookies));
        Cookies.Add(client, cookies);
        (await client.GetAsync(new Uri("/api/auth/me", UriKind.Relative), ct)).Dispose();
        return client;
    }

    /// <summary>A browser client signed in as a freshly registered user (unique email).</summary>
    public static async Task<HttpClient> RegisteredAsync(WebApplicationFactory<Program> factory, CancellationToken ct, string displayName = "Ada")
    {
        var client = await StartAsync(factory, ct);
        using var response = await client.PostAsJsonAsync(
            new Uri("/api/auth/register", UriKind.Relative),
            new { email = UniqueEmail(), password = "correct horse", displayName },
            ct);
        response.EnsureSuccessStatusCode();
        return client;
    }

    /// <summary>The <c>Cookie</c> header this client would send, e.g. for a SignalR connection made beside it.</summary>
    public static string CookieHeader(HttpClient client) =>
        Cookies.TryGetValue(client, out var cookies) ? cookies.GetCookieHeader(new Uri("http://localhost")) : "";

    public static string UniqueEmail() => $"user-{Guid.NewGuid():N}@example.com";

    /// <summary>The value of a cookie set by <paramref name="response"/>, or null.</summary>
    public static string? SetCookie(HttpResponseMessage response, string name) =>
        response.Headers.TryGetValues("Set-Cookie", out var cookies)
            ? cookies.FirstOrDefault(c => c.StartsWith(name + "=", StringComparison.Ordinal))
            : null;

    private sealed class XsrfHandler : DelegatingHandler
    {
        private string? _token;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var safe = request.Method == HttpMethod.Get || request.Method == HttpMethod.Head || request.Method == HttpMethod.Options;
            if (!safe && _token is not null)
            {
                request.Headers.TryAddWithoutValidation("X-XSRF-TOKEN", _token);
            }

            var response = await base.SendAsync(request, cancellationToken);
            if (SetCookie(response, "XSRF-TOKEN") is { } cookie)
            {
                var value = cookie["XSRF-TOKEN=".Length..].Split(';')[0];
                _token = Uri.UnescapeDataString(value);
            }

            return response;
        }
    }
}
