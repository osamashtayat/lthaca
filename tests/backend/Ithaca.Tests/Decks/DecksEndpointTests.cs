using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Ithaca.Domain.Films;
using Ithaca.Infrastructure.Persistence;
using Ithaca.Tests.Harness;
using Microsoft.Extensions.DependencyInjection;

namespace Ithaca.Tests.Decks;

/// <summary>
/// BE-08: "Just swipe" solo decks through the real API (api-contract.md §5), each test as a freshly registered user. The
/// shared database holds the dev seed, other tests' films, and the films added here (TMDB ids 79xxxx).
/// </summary>
[Collection(DatabaseCollectionDefinition.Name)]
public sealed class DecksEndpointTests(PostgresFixture postgres) : IAsyncLifetime
{
    private static readonly string[] Genres = ["Drama", "Comedy", "Horror", "Action", "Animation", "Documentary"];

    private static readonly string[] Cozy = ["cozy"];

    private IthacaApiFactory _factory = null!;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync()
    {
        _factory = await IthacaApiFactory.StartAsync(postgres.ConnectionString);
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        if (!db.Films.Any(f => f.TmdbId == 790000))
        {
            for (var i = 0; i < 36; i++)
            {
                var film = Film.Create(790000 + i, $"Deck film {i}", DateTimeOffset.UtcNow);
                film.UpdateDetails($"Deck film {i}", 1960 + (i * 2), "Overview", null, 85 + i, [Genres[i % Genres.Length]], 50 - i, null, 7, 100, "en", DateTimeOffset.UtcNow);
                db.Films.Add(film);
            }

            await db.SaveChangesAsync(Ct);
        }
    }

    public ValueTask DisposeAsync() => _factory.DisposeAsync();

    private static async Task<JsonElement> CreateAsync(HttpClient client)
    {
        using var response = await client.PostAsJsonAsync("/api/decks", new { source = "justSwipe", mood = (object?)null }, Ct);
        response.StatusCode.ShouldBe(HttpStatusCode.Created, await response.Content.ReadAsStringAsync(Ct));
        return await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
    }

    private static Task<HttpResponseMessage> SwipeAsync(HttpClient client, string deckId, string itemId, string direction, string? verdict = null, Guid? swipeId = null) =>
        client.PostAsJsonAsync($"/api/decks/{deckId}/swipes", new { swipeId = swipeId ?? Guid.NewGuid(), deckItemId = itemId, direction, seenVerdict = verdict }, Ct);

    private static async Task<string> CodeOf(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("code").GetString()!;

    private static List<JsonElement> Items(JsonElement deck) => deck.GetProperty("items").EnumerateArray().ToList();

    private static int TmdbId(JsonElement item) => item.GetProperty("film").GetProperty("tmdbId").GetInt32();

    [Fact]
    public async Task Create_JustSwipe_Returns201WithTheFirstBatch()
    {
        var client = await BrowserClient.RegisteredAsync(_factory, Ct);

        using var response = await client.PostAsJsonAsync("/api/decks", new { source = "justSwipe", mood = (object?)null }, Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var deck = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
        var id = deck.GetProperty("id").GetString();
        response.Headers.Location!.ToString().ShouldBe($"/api/decks/{id}");
        deck.GetProperty("source").GetString().ShouldBe("justSwipe");
        deck.GetProperty("mood").ValueKind.ShouldBe(JsonValueKind.Null);
        deck.GetProperty("swipeCount").GetInt32().ShouldBe(0);
        deck.GetProperty("yesCount").GetInt32().ShouldBe(0);
        var items = Items(deck);
        items.Count.ShouldBe(10);
        items.Select(i => i.GetProperty("position").GetInt32()).ShouldBe(Enumerable.Range(1, 10));
        items.ShouldAllBe(i => i.GetProperty("swipe").ValueKind == JsonValueKind.Null);
        items.Select(i => i.GetProperty("film").GetProperty("genres").EnumerateArray().Select(g => g.GetString()).FirstOrDefault()).Distinct().Count()
            .ShouldBeGreaterThanOrEqualTo(4, "the sampler varies genres");

        var again = await client.GetFromJsonAsync<JsonElement>($"/api/decks/{id}", Ct);
        Items(again).Select(TmdbId).ShouldBe(items.Select(TmdbId));
    }

    [Fact]
    public async Task Create_JustSwipeWithAMood_Is400OnMood()
    {
        var client = await BrowserClient.RegisteredAsync(_factory, Ct);

        using var response = await client.PostAsJsonAsync("/api/decks", new { source = "justSwipe", mood = new { moods = Cozy, maxRuntime = "any", language = (string?)null, text = (string?)null } }, Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await response.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("errors").TryGetProperty("mood", out _).ShouldBeTrue();
    }

    [Fact]
    public async Task Decks_AreForSignedInUsersOnly()
    {
        var anonymous = await BrowserClient.StartAsync(_factory, Ct);

        using var response = await anonymous.PostAsJsonAsync("/api/decks", new { source = "justSwipe", mood = (object?)null }, Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await CodeOf(response)).ShouldBe("auth.unauthenticated");
    }

    [Fact]
    public async Task AnotherUsersDeck_IsNotFound_ForEveryEndpoint()
    {
        var owner = await BrowserClient.RegisteredAsync(_factory, Ct);
        var other = await BrowserClient.RegisteredAsync(_factory, Ct);
        var deck = await CreateAsync(owner);
        var id = deck.GetProperty("id").GetString()!;
        var item = Items(deck)[0].GetProperty("id").GetString()!;

        using var get = await other.GetAsync(new Uri($"/api/decks/{id}", UriKind.Relative), Ct);
        using var more = await other.PostAsync(new Uri($"/api/decks/{id}/items", UriKind.Relative), null, Ct);
        using var swipe = await SwipeAsync(other, id, item, "right");
        using var top = await other.GetAsync(new Uri($"/api/decks/{id}/top", UriKind.Relative), Ct);

        foreach (var response in new[] { get, more, swipe, top })
        {
            response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
            (await CodeOf(response)).ShouldBe("deck.not_found");
        }
    }

    [Fact]
    public async Task Swipe_IsRecorded_AndARetryIsANoOp_ButAnotherSwipeIsAConflict()
    {
        var client = await BrowserClient.RegisteredAsync(_factory, Ct);
        var deck = await CreateAsync(client);
        var id = deck.GetProperty("id").GetString()!;
        var item = Items(deck)[0].GetProperty("id").GetString()!;
        var swipeId = Guid.NewGuid();

        using var first = await SwipeAsync(client, id, item, "right", swipeId: swipeId);
        using var retry = await SwipeAsync(client, id, item, "right", swipeId: swipeId);
        using var another = await SwipeAsync(client, id, item, "left");

        first.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        retry.StatusCode.ShouldBe(HttpStatusCode.NoContent, "the same swipeId is a no-op success");
        another.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await CodeOf(another)).ShouldBe("deck.already_swiped");

        var after = await client.GetFromJsonAsync<JsonElement>($"/api/decks/{id}", Ct);
        after.GetProperty("swipeCount").GetInt32().ShouldBe(1, "the retry didn't count twice");
        after.GetProperty("yesCount").GetInt32().ShouldBe(1);
        Items(after)[0].GetProperty("swipe").GetProperty("direction").GetString().ShouldBe("right");
    }

    [Fact]
    public async Task Swipe_Errors()
    {
        var client = await BrowserClient.RegisteredAsync(_factory, Ct);
        var deck = await CreateAsync(client);
        var id = deck.GetProperty("id").GetString()!;
        var item = Items(deck)[0].GetProperty("id").GetString()!;

        using var unknownItem = await SwipeAsync(client, id, Guid.NewGuid().ToString(), "right");
        using var verdictWithRight = await SwipeAsync(client, id, item, "right", "loved");
        using var unknownDeck = await SwipeAsync(client, Guid.NewGuid().ToString(), item, "right");

        unknownItem.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await CodeOf(unknownItem)).ShouldBe("deck.item_not_found");
        verdictWithRight.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await verdictWithRight.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("errors").TryGetProperty("seenVerdict", out _).ShouldBeTrue();
        unknownDeck.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await CodeOf(unknownDeck)).ShouldBe("deck.not_found");
    }

    [Fact]
    public async Task UpSwipes_UpdateTheLibrary_RightAndLeftDont()
    {
        var client = await BrowserClient.RegisteredAsync(_factory, Ct);
        var deck = await CreateAsync(client);
        var id = deck.GetProperty("id").GetString()!;
        var items = Items(deck);

        (await SwipeAsync(client, id, items[0].GetProperty("id").GetString()!, "up", "loved")).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await SwipeAsync(client, id, items[1].GetProperty("id").GetString()!, "up", "notForMe")).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await SwipeAsync(client, id, items[2].GetProperty("id").GetString()!, "right")).StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var loved = await client.GetFromJsonAsync<JsonElement>($"/api/library/{TmdbId(items[0])}", Ct);
        loved.GetProperty("seen").GetBoolean().ShouldBeTrue();
        loved.GetProperty("liked").GetBoolean().ShouldBeTrue();
        var notForMe = await client.GetFromJsonAsync<JsonElement>($"/api/library/{TmdbId(items[1])}", Ct);
        notForMe.GetProperty("seen").GetBoolean().ShouldBeTrue();
        notForMe.GetProperty("liked").GetBoolean().ShouldBeFalse();
        var right = await client.GetFromJsonAsync<JsonElement>($"/api/library/{TmdbId(items[2])}", Ct);
        right.GetProperty("seen").GetBoolean().ShouldBeFalse();
        right.GetProperty("onWatchlist").GetBoolean().ShouldBeFalse("a right swipe is not a watchlist add (A2)");
    }

    [Fact]
    public async Task Items_NeverRepeatAFilm_SkipSeenFilms_AndEndWithAnEmptyBatch()
    {
        var client = await BrowserClient.RegisteredAsync(_factory, Ct);
        using (var seen = await client.PutAsJsonAsync("/api/library/790001", new { liked = false, onWatchlist = true, seen = true, rating = (int?)null }, Ct))
        {
            seen.EnsureSuccessStatusCode();
        }

        var deck = await CreateAsync(client);
        var id = deck.GetProperty("id").GetString()!;
        var dealt = Items(deck).Select(TmdbId).ToList();
        var onWatchlist = new List<bool>();
        for (var batch = 0; batch < 100; batch++)
        {
            using var response = await client.PostAsync(new Uri($"/api/decks/{id}/items", UriKind.Relative), null, Ct);
            response.StatusCode.ShouldBe(HttpStatusCode.OK);
            var items = (await response.Content.ReadFromJsonAsync<JsonElement>(Ct)).EnumerateArray().ToList();
            if (items.Count == 0)
            {
                break;
            }

            dealt.AddRange(items.Select(TmdbId));
            onWatchlist.AddRange(items.Select(i => i.GetProperty("onWatchlist").GetBoolean()));
        }

        dealt.Count.ShouldBeGreaterThan(36, "the batches ran until the catalog was used up");
        dealt.Distinct().Count().ShouldBe(dealt.Count, "no film twice in a deck");
        dealt.ShouldNotContain(790001, "seen films are excluded");
        var full = await client.GetFromJsonAsync<JsonElement>($"/api/decks/{id}", Ct);
        Items(full).Select(i => i.GetProperty("position").GetInt32()).ShouldBe(Enumerable.Range(1, dealt.Count));
    }

    [Fact]
    public async Task Items_TwoConcurrentRequests_BothSucceed_WithoutDuplicates()
    {
        var client = await BrowserClient.RegisteredAsync(_factory, Ct);
        var deck = await CreateAsync(client);
        var id = deck.GetProperty("id").GetString()!;

        var responses = await Task.WhenAll(
            client.PostAsync(new Uri($"/api/decks/{id}/items", UriKind.Relative), null, Ct),
            client.PostAsync(new Uri($"/api/decks/{id}/items", UriKind.Relative), null, Ct));

        foreach (var r in responses)
        {
            r.StatusCode.ShouldBe(HttpStatusCode.OK, await r.Content.ReadAsStringAsync(Ct));
        }
        var full = await client.GetFromJsonAsync<JsonElement>($"/api/decks/{id}", Ct);
        var films = Items(full).Select(TmdbId).ToList();
        films.Distinct().Count().ShouldBe(films.Count);
        Items(full).Select(i => i.GetProperty("position").GetInt32()).ShouldBe(Enumerable.Range(1, films.Count));
        foreach (var response in responses)
        {
            response.Dispose();
        }
    }

    [Fact]
    public async Task Items_ShowTheCallersWatchlist()
    {
        var client = await BrowserClient.RegisteredAsync(_factory, Ct);
        var deck = await CreateAsync(client);
        var first = Items(deck)[0];
        using (var add = await client.PutAsync(new Uri($"/api/library/{TmdbId(first)}/watchlist", UriKind.Relative), null, Ct))
        {
            add.EnsureSuccessStatusCode();
        }

        var after = await client.GetFromJsonAsync<JsonElement>($"/api/decks/{deck.GetProperty("id").GetString()}", Ct);

        Items(after)[0].GetProperty("onWatchlist").GetBoolean().ShouldBeTrue();
        Items(after).Skip(1).ShouldAllBe(i => !i.GetProperty("onWatchlist").GetBoolean());
    }

    [Fact]
    public async Task Top_WithoutYeses_Is422_ThenListsUpToThreeRightSwipes()
    {
        var client = await BrowserClient.RegisteredAsync(_factory, Ct);
        var deck = await CreateAsync(client);
        var id = deck.GetProperty("id").GetString()!;
        var items = Items(deck);
        (await SwipeAsync(client, id, items[0].GetProperty("id").GetString()!, "left")).EnsureSuccessStatusCode();

        using var none = await client.GetAsync(new Uri($"/api/decks/{id}/top", UriKind.Relative), Ct);
        none.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        (await CodeOf(none)).ShouldBe("deck.no_yeses");

        var yes = items.Skip(1).Take(4).ToList();
        foreach (var item in yes)
        {
            (await SwipeAsync(client, id, item.GetProperty("id").GetString()!, "right")).EnsureSuccessStatusCode();
        }

        var top = await client.GetFromJsonAsync<JsonElement>($"/api/decks/{id}/top", Ct);
        top.GetProperty("swipeCount").GetInt32().ShouldBe(5);
        top.GetProperty("yesCount").GetInt32().ShouldBe(4);
        var picks = top.GetProperty("picks").EnumerateArray().ToList();
        picks.Count.ShouldBe(3);
        picks.Select(p => p.GetProperty("film").GetProperty("tmdbId").GetInt32()).ShouldBeSubsetOf(yes.Select(TmdbId));
        picks.ShouldAllBe(p => p.GetProperty("reason").ValueKind == JsonValueKind.Null);
    }
}
