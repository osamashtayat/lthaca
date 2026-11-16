using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Ithaca.Application.Common;
using Ithaca.Application.Tmdb;
using Ithaca.Tests.Harness;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Sentry;
using Sentry.AspNetCore;
using Sentry.Extensibility;
using Sentry.Protocol.Envelopes;

namespace Ithaca.Tests.Monitoring;

/// <summary>
/// BE-21 against the real pipeline (ADR-0028): with a DSN, an unhandled exception from a signed-in request reaches Sentry
/// with its message, route and the internal user id, and nothing personal (no email, cookies or antiforgery token). Events
/// go to a capturing transport, never the network. Without a DSN the SDK is off.
/// <para>This proves the end result of the whole setup (SDK defaults plus <c>SentryScrubber</c>); the scrubber's own rules,
/// such as dropping <c>X-XSRF-TOKEN</c> on writes and query strings on outgoing calls, are unit-tested in
/// <see cref="SentryScrubberTests"/>.</para>
/// </summary>
[Collection(DatabaseCollectionDefinition.Name)]
public sealed class ErrorTrackingTests(PostgresFixture postgres)
{
    private const string FakeDsn = "https://public@sentry.invalid/1";

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task UnhandledException_ReachesSentry_WithTheUserIdAndNothingPersonal()
    {
        var transport = new CapturingTransport();
        // A fresh database, so film 550 has no cached trailer and the endpoint asks the (throwing) TMDB.
        await using var factory = await IthacaApiFactory.StartAsync(await postgres.CreateDatabaseAsync(Ct));
        await using var sentryOn = factory.WithWebHostBuilder(b => b.ConfigureTestServices(s =>
        {
            s.AddSingleton<ITmdbClient>(new ThrowingTmdb());
            s.PostConfigure<SentryAspNetCoreOptions>(o =>
            {
                o.Dsn = FakeDsn;
                o.Transport = transport;
            });
        }));
        using var user = await BrowserClient.RegisteredAsync(sentryOn, Ct);
        var me = await user.GetFromJsonAsync<JsonElement>(new Uri("/api/auth/me", UriKind.Relative), Ct);
        var userId = me.GetProperty("id").GetString()!;
        var email = me.GetProperty("email").GetString()!;
        var cookieValues = BrowserClient.CookieHeader(user).Split(';', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Select(c => c[(c.IndexOf('=', StringComparison.Ordinal) + 1)..]).Where(v => v.Length > 8).ToList();

        using var response = await user.GetAsync(new Uri("/api/films/550/trailer", UriKind.Relative), Ct);
        response.StatusCode.ShouldBe(HttpStatusCode.InternalServerError);

        var sent = await transport.WaitForAsync(ThrowingTmdb.Message, Ct);
        sent.ShouldContain(userId, Case.Insensitive, "the internal id identifies the user");
        sent.ShouldContain("/api/films/550/trailer");
        sent.ShouldContain("\"environment\":\"development\"");
        sent.ShouldNotContain(email, Case.Insensitive);
        sent.ShouldNotContain("\"cookies\"", Case.Insensitive);
        cookieValues.ShouldNotBeEmpty();
        cookieValues.ShouldAllBe(value => !sent.Contains(value, StringComparison.Ordinal), "no cookie value leaves the server");
    }

    [Fact]
    public async Task WithoutADsn_SentryIsOff()
    {
        await using var factory = await IthacaApiFactory.StartAsync(postgres.ConnectionString);

        factory.Services.GetRequiredService<IHub>().IsEnabled.ShouldBeFalse();
    }

    /// <summary>Keeps every envelope as JSON text instead of sending it.</summary>
    private sealed class CapturingTransport : ITransport
    {
        private readonly ConcurrentQueue<string> _envelopes = new();

        public async Task SendEnvelopeAsync(Envelope envelope, CancellationToken cancellationToken = default)
        {
            using var stream = new MemoryStream();
            await envelope.SerializeAsync(stream, null, cancellationToken);
            _envelopes.Enqueue(Encoding.UTF8.GetString(stream.ToArray()));
        }

        /// <summary>The first envelope containing <paramref name="text"/>; events are sent in the background.</summary>
        public async Task<string> WaitForAsync(string text, CancellationToken ct)
        {
            for (var i = 0; i < 100; i++)
            {
                var match = _envelopes.FirstOrDefault(e => e.Contains(text, StringComparison.Ordinal));
                if (match is not null)
                {
                    return match;
                }

                await Task.Delay(50, ct);
            }

            throw new ShouldAssertException($"No Sentry envelope contained '{text}'. Sent: {_envelopes.Count}.");
        }
    }

    private sealed class ThrowingTmdb : ITmdbClient
    {
        public const string Message = "error-tracking test: TMDB blew up";

        public Task<Result<IReadOnlyList<TmdbFilmSummary>>> GetCatalogPageAsync(int page, CancellationToken ct) => throw new InvalidOperationException(Message);

        public Task<Result<TmdbFilmDetails?>> GetFilmDetailsAsync(int tmdbId, CancellationToken ct) => throw new InvalidOperationException(Message);

        public Task<Result<IReadOnlyList<TmdbVideo>>> GetTrailersAsync(int tmdbId, CancellationToken ct) => throw new InvalidOperationException(Message);

        public Task<Result<IReadOnlyList<TmdbSearchResult>>> SearchFilmsAsync(string title, int? year, CancellationToken ct) => throw new InvalidOperationException(Message);
    }
}
