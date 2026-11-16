using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Ithaca.Tests.Harness;
using Ithaca.Tests.Imports;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;

namespace Ithaca.Tests.Common;

/// <summary>
/// BE-19: request rate limits through the real API on a fake clock (api-contract.md §2). Over a limit → 429
/// <c>rate.limited</c> with <c>Retry-After</c>; each person counts separately; requests without a session count by IP.
/// </summary>
[Collection(DatabaseCollectionDefinition.Name)]
public sealed class RateLimitTests(PostgresFixture postgres) : IAsyncLifetime
{
    private static readonly Uri SignIn = new("/api/auth/sign-in", UriKind.Relative);
    private static readonly Uri Decks = new("/api/decks", UriKind.Relative);

    private readonly FakeTimeProvider _time = new(DateTimeOffset.UtcNow);
    private IthacaApiFactory _base = null!;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync() =>
        _base = await IthacaApiFactory.StartAsync(postgres.ConnectionString);

    public async ValueTask DisposeAsync() => await _base.DisposeAsync();

    /// <summary>The API with limits on, the given settings, and the fake clock.</summary>
    private WebApplicationFactory<Program> Limited(params (string Key, string Value)[] settings) =>
        _base.WithWebHostBuilder(b =>
        {
            b.UseSetting("RateLimits:Enabled", "true");
            foreach (var (key, value) in settings)
            {
                b.UseSetting(key, value);
            }

            b.ConfigureTestServices(s => s.AddSingleton<TimeProvider>(_time));
        });

    private static async Task AssertRateLimitedAsync(HttpResponseMessage response, int retryAfterSeconds)
    {
        response.StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);
        response.Headers.RetryAfter.ShouldNotBeNull();
        response.Headers.RetryAfter.Delta.ShouldBe(TimeSpan.FromSeconds(retryAfterSeconds));
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
        body.GetProperty("code").GetString().ShouldBe("rate.limited");
        body.GetProperty("status").GetInt32().ShouldBe(429);
    }

    [Fact]
    public async Task SignIn_OverTheLimit_Is429WithRetryAfter_UntilTheWindowEnds()
    {
        await using var app = Limited(("RateLimits:Auth:PermitLimit", "3"), ("RateLimits:Auth:Window", "00:15:00"));
        var client = await BrowserClient.StartAsync(app, Ct);
        var wrong = new { email = BrowserClient.UniqueEmail(), password = "wrong password" };

        for (var i = 0; i < 3; i++)
        {
            using var allowed = await client.PostAsJsonAsync(SignIn, wrong, Ct);
            allowed.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        }

        _time.Advance(TimeSpan.FromMinutes(5));
        using (var refused = await client.PostAsJsonAsync(SignIn, wrong, Ct))
        {
            await AssertRateLimitedAsync(refused, retryAfterSeconds: 600);
        }

        _time.Advance(TimeSpan.FromMinutes(10));
        using var again = await client.PostAsJsonAsync(SignIn, wrong, Ct);
        again.StatusCode.ShouldBe(HttpStatusCode.Unauthorized, "a new window");
    }

    [Fact]
    public async Task DeckCreation_CountsPerUser()
    {
        await using var app = Limited(("RateLimits:DeckCreate:PermitLimit", "2"), ("RateLimits:DeckCreate:Window", "01:00:00"));
        var ada = await BrowserClient.RegisteredAsync(app, Ct);
        var bea = await BrowserClient.RegisteredAsync(app, Ct, "Bea");
        var body = new { source = "justSwipe", mood = (object?)null };

        for (var i = 0; i < 2; i++)
        {
            using var created = await ada.PostAsJsonAsync(Decks, body, Ct);
            created.StatusCode.ShouldBe(HttpStatusCode.Created);
        }

        using (var refused = await ada.PostAsJsonAsync(Decks, body, Ct))
        {
            await AssertRateLimitedAsync(refused, retryAfterSeconds: 3600);
        }

        using (var other = await bea.PostAsJsonAsync(Decks, body, Ct))
        {
            other.StatusCode.ShouldBe(HttpStatusCode.Created, "another person has their own count");
        }

        _time.Advance(TimeSpan.FromHours(1));
        using var later = await ada.PostAsJsonAsync(Decks, body, Ct);
        later.StatusCode.ShouldBe(HttpStatusCode.Created);
    }

    [Fact]
    public async Task ImportCreation_DefaultsToFivePerUserPerDay()
    {
        await using var app = Limited();
        var client = await BrowserClient.RegisteredAsync(app, Ct);
        var notAnExport = Encoding.UTF8.GetBytes("hello");

        for (var i = 0; i < 5; i++)
        {
            using var counted = await client.PostAsync(new Uri("/api/imports", UriKind.Relative), ImportTestKit.Form(notAnExport, "x.csv"), Ct);
            counted.StatusCode.ShouldBe(HttpStatusCode.BadRequest, "refused uploads count too");
        }

        using var refused = await client.PostAsync(new Uri("/api/imports", UriKind.Relative), ImportTestKit.Form(notAnExport, "x.csv"), Ct);
        await AssertRateLimitedAsync(refused, retryAfterSeconds: 24 * 3600);
    }

    [Fact]
    public async Task AiBackedCalls_AreLimited()
    {
        await using var app = Limited(("RateLimits:Ai:PermitLimit", "1"), ("RateLimits:Ai:Window", "00:10:00"));
        var client = await BrowserClient.RegisteredAsync(app, Ct);
        using var created = await client.PostAsJsonAsync(Decks, new { source = "justSwipe", mood = (object?)null }, Ct);
        var deckId = (await created.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("id").GetString();

        using var first = await client.GetAsync(new Uri($"/api/decks/{deckId}/top", UriKind.Relative), Ct);
        using var second = await client.GetAsync(new Uri($"/api/decks/{deckId}/top", UriKind.Relative), Ct);

        first.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity, "deck.no_yeses: it still counts");
        await AssertRateLimitedAsync(second, retryAfterSeconds: 600);
    }

    [Fact]
    public async Task RoomJoin_CountsByIp_SoFreshGuestsShareOneCount()
    {
        await using var app = Limited(("RateLimits:RoomJoin:PermitLimit", "2"), ("RateLimits:RoomJoin:Window", "00:10:00"));
        var host = await BrowserClient.RegisteredAsync(app, Ct);
        using var created = await host.PostAsJsonAsync(new Uri("/api/rooms", UriKind.Relative), new { name = "Friday", mode = "justSwipe", startsAt = (string?)null }, Ct);
        var code = (await created.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("code").GetString();
        var join = new Uri($"/api/rooms/{code}/join", UriKind.Relative);

        for (var i = 0; i < 2; i++)
        {
            var guest = await BrowserClient.StartAsync(app, Ct);
            using var joined = await guest.PostAsJsonAsync(join, new { displayName = $"Guest {i}" }, Ct);
            joined.StatusCode.ShouldBe(HttpStatusCode.OK);
        }

        var another = await BrowserClient.StartAsync(app, Ct);
        using var refused = await another.PostAsJsonAsync(join, new { displayName = "Guest 3" }, Ct);
        await AssertRateLimitedAsync(refused, retryAfterSeconds: 600);
    }

    [Fact]
    public async Task ARuleWithoutALimit_FailsAtStartup()
    {
        await using var app = Limited(("RateLimits:Auth:PermitLimit", "0"));

        var error = Should.Throw<Exception>(() => app.Services.GetRequiredService<IServiceProvider>());

        error.ToString().ShouldContain("every rule needs PermitLimit");
    }

    [Fact]
    public async Task WithLimitsOff_NothingIsLimited()
    {
        await using var app = _base.WithWebHostBuilder(b => b
            .UseSetting("RateLimits:Auth:PermitLimit", "1")
            .ConfigureTestServices(s => s.AddSingleton<TimeProvider>(_time)));
        var client = await BrowserClient.StartAsync(app, Ct);
        var wrong = new { email = BrowserClient.UniqueEmail(), password = "wrong password" };

        for (var i = 0; i < 3; i++)
        {
            using var response = await client.PostAsJsonAsync(SignIn, wrong, Ct);
            response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        }
    }
}
