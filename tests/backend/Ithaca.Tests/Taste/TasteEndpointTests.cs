using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Ithaca.Application.Ai;
using Ithaca.Domain.Decks;
using Ithaca.Domain.Films;
using Ithaca.Infrastructure.Ai;
using Ithaca.Infrastructure.Persistence;
using Ithaca.Tests.Harness;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Ithaca.Tests.Taste;

/// <summary>
/// BE-14: taste through the real API (api-contract.md §12): readiness and the receipt, decks by taste, group taste rooms.
/// The films added here (TMDB ids 780xxx) carry Fake embeddings in two clusters: "Science Fiction" films close to one
/// direction, "Western" films close to another, so a taste built from the first cluster must pull its films forward.
/// </summary>
[Collection(DatabaseCollectionDefinition.Name)]
public sealed class TasteEndpointTests(PostgresFixture postgres) : IAsyncLifetime
{
    private const int SciFiBase = 780000;
    private const int WesternBase = 780100;
    private const int ClusterSize = 24;

    private static readonly string[] Cozy = ["cozy"];

    private IthacaApiFactory _factory = null!;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static IEnumerable<int> SciFi => Enumerable.Range(SciFiBase, ClusterSize);

    private static IEnumerable<int> Rated => SciFi.Take(10);

    public async ValueTask InitializeAsync()
    {
        _factory = await IthacaApiFactory.StartAsync(postgres.ConnectionString);
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        if (await db.Films.AnyAsync(f => f.TmdbId == SciFiBase, Ct))
        {
            return;
        }

        var fake = new FakeEmbeddingClient();
        var texts = new List<string> { "cluster sci-fi", "cluster western" };
        texts.AddRange(Enumerable.Range(0, ClusterSize * 2).Select(i => $"noise {i}"));
        var vectors = (await fake.EmbedAsync(texts, EmbeddingInputType.Document, Ct)).Value;
        var now = DateTimeOffset.UtcNow;
        for (var i = 0; i < ClusterSize * 2; i++)
        {
            var sciFi = i < ClusterSize;
            var tmdbId = sciFi ? SciFiBase + i : WesternBase + i - ClusterSize;
            var film = Film.Create(tmdbId, $"Taste film {tmdbId}", now);
            film.UpdateDetails($"Taste film {tmdbId}", 1970 + i, "Overview", null, 100, [sciFi ? "Science Fiction" : "Western"], 20 + (i % 7), null, 7, 100, "en", now);
            var centre = vectors[sciFi ? 0 : 1];
            var noise = vectors[2 + i];
            film.SetEmbedding(centre.Zip(noise, (c, n) => (c * 0.9f) + (n * 0.3f)).ToArray(), fake.ModelName, new string('a', Film.EmbeddingSourceHashLength));
            db.Films.Add(film);
        }

        await db.SaveChangesAsync(Ct);
    }

    public ValueTask DisposeAsync() => _factory.DisposeAsync();

    private static async Task PutAsync(HttpClient client, int tmdbId, bool liked, bool seen, decimal? rating)
    {
        using var response = await client.PutAsJsonAsync($"/api/library/{tmdbId}", new { liked, onWatchlist = false, seen, rating }, Ct);
        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync(Ct));
    }

    /// <summary>Rates the first ten sci-fi films 5 stars (ten positive signals): ready.</summary>
    private static async Task MakeReadyAsync(HttpClient client)
    {
        foreach (var tmdbId in Rated)
        {
            await PutAsync(client, tmdbId, liked: false, seen: true, rating: 5);
        }
    }

    private static async Task<string> CodeOf(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("code").GetString()!;

    private static int TmdbId(JsonElement item) => item.GetProperty("film").GetProperty("tmdbId").GetInt32();

    private static Task<HttpResponseMessage> CreateTasteDeckAsync(HttpClient client, object? mood = null) =>
        client.PostAsJsonAsync("/api/decks", new { source = "taste", mood }, Ct);

    private static Task<HttpResponseMessage> CreateRoomAsync(HttpClient client, string mode, string name = "Blend night") =>
        client.PostAsJsonAsync("/api/rooms", new { name, mode, startsAt = (string?)null }, Ct);

    private async Task<int> TasteDeckCountAsync()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<AppDbContext>().Decks.CountAsync(d => d.Source == DeckSource.Taste, Ct);
    }

    [Fact]
    public async Task GetTaste_AnonymousOrGuest_IsUnauthenticated()
    {
        using var host = await BrowserClient.RegisteredAsync(_factory, Ct);
        using var created = await CreateRoomAsync(host, "justSwipe");
        var code = (await created.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("code").GetString();
        using var guest = await BrowserClient.StartAsync(_factory, Ct);
        (await guest.PostAsJsonAsync($"/api/rooms/{code}/join", new { displayName = "Omar" }, Ct)).EnsureSuccessStatusCode();
        using var anonymous = await BrowserClient.StartAsync(_factory, Ct);

        foreach (var client in new[] { anonymous, guest })
        {
            using var response = await client.GetAsync(new Uri("/api/taste", UriKind.Relative), Ct);
            response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        }
    }

    [Fact]
    public async Task GetTaste_WithLittleHistory_IsNotReady_AndCountsSignalsAndLoggedFilms()
    {
        using var user = await BrowserClient.RegisteredAsync(_factory, Ct);
        await PutAsync(user, SciFiBase, liked: true, seen: false, rating: null);     // liked: a signal, not logged
        await PutAsync(user, SciFiBase + 1, liked: false, seen: true, rating: 3.5m); // rated 3.5: a signal (A16)
        await PutAsync(user, SciFiBase + 2, liked: false, seen: true, rating: 3);    // rated 3: logged, no signal
        await PutAsync(user, SciFiBase + 3, liked: false, seen: true, rating: null); // seen: logged, no signal

        var taste = await user.GetFromJsonAsync<JsonElement>("/api/taste", Ct);

        taste.GetProperty("ready").GetBoolean().ShouldBeFalse();
        taste.GetProperty("signalCount").GetInt32().ShouldBe(2);
        taste.GetProperty("signalsNeeded").GetInt32().ShouldBe(10);
        taste.GetProperty("loggedCount").GetInt32().ShouldBe(3);
        taste.GetProperty("topGenres").GetArrayLength().ShouldBe(0);
        taste.GetProperty("notForYou").GetArrayLength().ShouldBe(0);
    }

    [Fact]
    public async Task GetTaste_WhenReady_ShowsTheReceipt()
    {
        using var user = await BrowserClient.RegisteredAsync(_factory, Ct);
        await MakeReadyAsync(user);
        await PutAsync(user, WesternBase, liked: false, seen: true, rating: 1);
        await PutAsync(user, WesternBase + 1, liked: false, seen: true, rating: 2);

        var taste = await user.GetFromJsonAsync<JsonElement>("/api/taste", Ct);

        taste.GetProperty("ready").GetBoolean().ShouldBeTrue();
        taste.GetProperty("signalCount").GetInt32().ShouldBe(10);
        taste.GetProperty("loggedCount").GetInt32().ShouldBe(12);
        var top = taste.GetProperty("topGenres").EnumerateArray().ToList();
        top.Count.ShouldBe(1);
        top[0].GetProperty("genre").GetString().ShouldBe("Science Fiction");
        top[0].GetProperty("percent").GetInt32().ShouldBe(100);
        taste.GetProperty("notForYou").EnumerateArray().Select(g => g.GetString()).ShouldBe(["Western"]);
    }

    [Fact]
    public async Task TasteDeck_NotReady_Is422AndCreatesNothing()
    {
        using var user = await BrowserClient.RegisteredAsync(_factory, Ct);
        await PutAsync(user, SciFiBase, liked: true, seen: false, rating: null);
        var before = await TasteDeckCountAsync();

        using var response = await CreateTasteDeckAsync(user);

        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        (await CodeOf(response)).ShouldBe("taste.not_ready");
        (await TasteDeckCountAsync()).ShouldBe(before);
    }

    [Fact]
    public async Task TasteDeck_WithAMood_Is400BeforeReadiness()
    {
        using var user = await BrowserClient.RegisteredAsync(_factory, Ct); // not ready: the 400 still wins (§12)

        using var response = await CreateTasteDeckAsync(user, new { moods = Cozy, maxRuntime = "any", language = (string?)null, text = (string?)null });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await response.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("errors").TryGetProperty("mood", out _).ShouldBeTrue();
    }

    [Fact]
    public async Task TasteDeck_WhenReady_LeansTowardsTheTaste_AndSkipsSeenFilms()
    {
        using var user = await BrowserClient.RegisteredAsync(_factory, Ct);
        await MakeReadyAsync(user);

        using var response = await CreateTasteDeckAsync(user);

        response.StatusCode.ShouldBe(HttpStatusCode.Created, await response.Content.ReadAsStringAsync(Ct));
        var deck = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
        deck.GetProperty("source").GetString().ShouldBe("taste");
        deck.GetProperty("mood").ValueKind.ShouldBe(JsonValueKind.Null);
        var first = deck.GetProperty("items").EnumerateArray().Select(TmdbId).ToList();
        first.Count.ShouldBe(10);
        first.ShouldNotContain(id => Rated.Contains(id), "films the user has seen are skipped");
        first.Count(id => SciFi.Contains(id)).ShouldBeGreaterThanOrEqualTo(7, "the exploited share is the closest to the taste");

        var deckId = deck.GetProperty("id").GetString();
        using var more = await user.PostAsync(new Uri($"/api/decks/{deckId}/items", UriKind.Relative), null, Ct);
        more.StatusCode.ShouldBe(HttpStatusCode.OK);
        var next = (await more.Content.ReadFromJsonAsync<JsonElement>(Ct)).EnumerateArray().Select(TmdbId).ToList();
        next.ShouldNotBeEmpty();
        next.ShouldNotContain(id => first.Contains(id) || Rated.Contains(id));
    }

    [Fact]
    public async Task TasteRoom_HostNotReady_Is422_AndABlankNameIs400First()
    {
        using var user = await BrowserClient.RegisteredAsync(_factory, Ct);

        using var notReady = await CreateRoomAsync(user, "taste");
        using var blank = await CreateRoomAsync(user, "taste", name: "  ");

        notReady.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        (await CodeOf(notReady)).ShouldBe("taste.not_ready");
        blank.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task TasteRoom_BlendsTheMembers_AndSkipsFilmsAnyMemberHasSeen()
    {
        using var host = await BrowserClient.RegisteredAsync(_factory, Ct);
        await MakeReadyAsync(host);
        using var created = await CreateRoomAsync(host, "taste");
        created.StatusCode.ShouldBe(HttpStatusCode.Created, await created.Content.ReadAsStringAsync(Ct));
        var room = await created.Content.ReadFromJsonAsync<JsonElement>(Ct);
        room.GetProperty("mode").GetString().ShouldBe("taste");
        room.GetProperty("me").GetProperty("isReady").GetBoolean().ShouldBeTrue();
        var code = room.GetProperty("code").GetString();

        using var friend = await BrowserClient.RegisteredAsync(_factory, Ct, "Lina");
        var friendSeen = SciFi.Skip(10).Take(6).ToList();
        foreach (var tmdbId in friendSeen)
        {
            await PutAsync(friend, tmdbId, liked: false, seen: true, rating: null); // seen, no taste
        }

        (await friend.PostAsJsonAsync($"/api/rooms/{code}/join", new { displayName = (string?)null }, Ct)).EnsureSuccessStatusCode();
        using var guest = await BrowserClient.StartAsync(_factory, Ct);
        (await guest.PostAsJsonAsync($"/api/rooms/{code}/join", new { displayName = "Omar" }, Ct)).EnsureSuccessStatusCode();

        using var start = await host.PostAsync(new Uri($"/api/rooms/{code}/start", UriKind.Relative), null, Ct);

        start.StatusCode.ShouldBe(HttpStatusCode.OK, await start.Content.ReadAsStringAsync(Ct));
        var cards = (await guest.GetFromJsonAsync<JsonElement>($"/api/rooms/{code}/deck", Ct)).EnumerateArray().Select(TmdbId).ToList();
        cards.Count.ShouldBe(10);
        cards.ShouldNotContain(id => Rated.Contains(id) || friendSeen.Contains(id), "a film any member has seen is skipped");
        cards.Count(id => SciFi.Contains(id)).ShouldBeGreaterThanOrEqualTo(7, "the host's taste leads; the friend and guest add none");
    }
}
