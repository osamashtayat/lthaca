using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Ithaca.Tests.Harness;

namespace Ithaca.Tests.Library;

/// <summary>BE-05: My Cinema endpoints (api-contract.md §7) through the real API, each test as a freshly registered user.</summary>
[Collection(DatabaseCollectionDefinition.Name)]
public sealed class LibraryEndpointTests(PostgresFixture postgres) : IAsyncLifetime
{
    private IthacaApiFactory _factory = null!;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync() => _factory = await IthacaApiFactory.StartAsync(postgres.ConnectionString);

    public ValueTask DisposeAsync() => _factory.DisposeAsync();

    private static Task<HttpResponseMessage> PutAsync(HttpClient client, int tmdbId, bool liked = false, bool onWatchlist = false, bool seen = false, int? rating = null) =>
        client.PutAsJsonAsync($"/api/library/{tmdbId}", new { liked, onWatchlist, seen, rating }, Ct);

    private static async Task<JsonElement> JsonAsync(HttpResponseMessage response) =>
        await response.Content.ReadFromJsonAsync<JsonElement>(Ct);

    [Fact]
    public async Task Get_UntouchedFilm_IsAllFalseWithTheFilm()
    {
        var client = await BrowserClient.RegisteredAsync(_factory, Ct);

        var body = await client.GetFromJsonAsync<JsonElement>("/api/library/550", Ct);

        body.GetProperty("liked").GetBoolean().ShouldBeFalse();
        body.GetProperty("onWatchlist").GetBoolean().ShouldBeFalse();
        body.GetProperty("seen").GetBoolean().ShouldBeFalse();
        body.GetProperty("rating").ValueKind.ShouldBe(JsonValueKind.Null);
        body.GetProperty("film").GetProperty("tmdbId").GetInt32().ShouldBe(550);
        body.GetProperty("film").GetProperty("title").GetString().ShouldNotBeNullOrWhiteSpace();
        body.GetProperty("film").TryGetProperty("overview", out _).ShouldBeFalse("lists and library use FilmSummaryDto");
    }

    [Fact]
    public async Task Put_ReplacesTheState_AndARatingImpliesSeen()
    {
        var client = await BrowserClient.RegisteredAsync(_factory, Ct);

        using var put = await PutAsync(client, 550, liked: true, seen: false, rating: 4);

        put.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await JsonAsync(put);
        body.GetProperty("liked").GetBoolean().ShouldBeTrue();
        body.GetProperty("seen").GetBoolean().ShouldBeTrue("a rating implies seen (A8)");
        body.GetProperty("rating").GetInt32().ShouldBe(4);

        using var replaced = await PutAsync(client, 550, onWatchlist: true);
        var after = await JsonAsync(replaced);
        after.GetProperty("liked").GetBoolean().ShouldBeFalse("PUT replaces the whole state");
        after.GetProperty("onWatchlist").GetBoolean().ShouldBeTrue();
        after.GetProperty("rating").ValueKind.ShouldBe(JsonValueKind.Null);
        (await client.GetFromJsonAsync<JsonElement>("/api/library/550", Ct)).GetProperty("onWatchlist").GetBoolean().ShouldBeTrue();
    }

    [Fact]
    public async Task Put_AllFalse_DeletesTheRow()
    {
        var client = await BrowserClient.RegisteredAsync(_factory, Ct);
        (await PutAsync(client, 550, liked: true)).Dispose();

        using var cleared = await PutAsync(client, 550);

        cleared.StatusCode.ShouldBe(HttpStatusCode.OK);
        var summary = await client.GetFromJsonAsync<JsonElement>("/api/library/summary", Ct);
        summary.GetProperty("liked").GetInt32().ShouldBe(0);
        (await client.GetFromJsonAsync<JsonElement>("/api/library?shelf=liked", Ct)).GetProperty("totalCount").GetInt32().ShouldBe(0);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(6)]
    public async Task Put_RatingOutOfRange_Is400(int rating)
    {
        var client = await BrowserClient.RegisteredAsync(_factory, Ct);

        using var response = await PutAsync(client, 550, rating: rating);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await JsonAsync(response)).GetProperty("code").GetString().ShouldBe("validation");
    }

    [Fact]
    public async Task UnknownFilm_Is404FilmNotFound_OnEveryEndpoint()
    {
        var client = await BrowserClient.RegisteredAsync(_factory, Ct);

        HttpResponseMessage[] responses =
        [
            await client.GetAsync("/api/library/999999", Ct),
            await PutAsync(client, 999999, liked: true),
            await client.PutAsync("/api/library/999999/watchlist", null, Ct),
            await client.DeleteAsync("/api/library/999999/watchlist", Ct),
        ];

        foreach (var response in responses)
        {
            response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
            (await JsonAsync(response)).GetProperty("code").GetString().ShouldBe("film.not_found");
            response.Dispose();
        }
    }

    [Fact]
    public async Task Watchlist_AddAndRemove_AreIdempotent_AndLeaveOtherFlagsAlone()
    {
        var client = await BrowserClient.RegisteredAsync(_factory, Ct);
        (await PutAsync(client, 27205, liked: true, rating: 5)).Dispose();

        for (var i = 0; i < 2; i++)
        {
            using var added = await client.PutAsync("/api/library/27205/watchlist", null, Ct);
            added.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        }

        var state = await client.GetFromJsonAsync<JsonElement>("/api/library/27205", Ct);
        state.GetProperty("onWatchlist").GetBoolean().ShouldBeTrue();
        state.GetProperty("liked").GetBoolean().ShouldBeTrue();
        state.GetProperty("rating").GetInt32().ShouldBe(5);

        for (var i = 0; i < 2; i++)
        {
            using var removed = await client.DeleteAsync("/api/library/27205/watchlist", Ct);
            removed.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        }

        var after = await client.GetFromJsonAsync<JsonElement>("/api/library/27205", Ct);
        after.GetProperty("onWatchlist").GetBoolean().ShouldBeFalse();
        after.GetProperty("liked").GetBoolean().ShouldBeTrue();
    }

    [Fact]
    public async Task Watchlist_RemoveOnUntouchedFilm_Is204_AndAddCreatesTheRow()
    {
        var client = await BrowserClient.RegisteredAsync(_factory, Ct);

        using var removed = await client.DeleteAsync("/api/library/496243/watchlist", Ct);
        removed.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        using var added = await client.PutAsync("/api/library/496243/watchlist", null, Ct);
        added.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var shelf = await client.GetFromJsonAsync<JsonElement>("/api/library?shelf=watchlist", Ct);
        shelf.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("film").GetProperty("tmdbId").GetInt32()).ShouldBe([496243]);

        using var lastFlagGone = await client.DeleteAsync("/api/library/496243/watchlist", Ct);
        (await client.GetFromJsonAsync<JsonElement>("/api/library/summary", Ct)).GetProperty("watchlist").GetInt32().ShouldBe(0);
    }

    [Fact]
    public async Task Summary_CountsEachShelf()
    {
        var client = await BrowserClient.RegisteredAsync(_factory, Ct);
        (await PutAsync(client, 550, liked: true, rating: 4)).Dispose();
        (await PutAsync(client, 27205, onWatchlist: true)).Dispose();
        (await PutAsync(client, 496243, liked: true, onWatchlist: true)).Dispose();

        var summary = await client.GetFromJsonAsync<JsonElement>("/api/library/summary", Ct);

        summary.GetProperty("liked").GetInt32().ShouldBe(2);
        summary.GetProperty("watchlist").GetInt32().ShouldBe(2);
        summary.GetProperty("seen").GetInt32().ShouldBe(1);
    }

    [Fact]
    public async Task List_FiltersByShelfAndGenre_SortsAndPages()
    {
        var client = await BrowserClient.RegisteredAsync(_factory, Ct);
        (await PutAsync(client, 550, liked: true, rating: 3)).Dispose();     // Fight Club: Drama
        (await PutAsync(client, 27205, liked: true)).Dispose();               // Inception: Action, Science Fiction
        (await PutAsync(client, 496243, liked: true, rating: 5)).Dispose();   // Parasite: Comedy, Thriller, Drama

        static int[] Ids(JsonElement page) =>
            [.. page.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("film").GetProperty("tmdbId").GetInt32())];

        Ids(await client.GetFromJsonAsync<JsonElement>("/api/library?shelf=liked&sort=recent", Ct)).ShouldBe([496243, 27205, 550]);
        Ids(await client.GetFromJsonAsync<JsonElement>("/api/library?shelf=liked&sort=title", Ct)).ShouldBe([550, 27205, 496243]);
        Ids(await client.GetFromJsonAsync<JsonElement>("/api/library?shelf=liked&sort=rating", Ct)).ShouldBe([496243, 550, 27205], "unrated last");
        Ids(await client.GetFromJsonAsync<JsonElement>("/api/library?shelf=liked&genre=Drama&sort=title", Ct)).ShouldBe([550, 496243]);
        Ids(await client.GetFromJsonAsync<JsonElement>("/api/library?shelf=seen&sort=title", Ct)).ShouldBe([550, 496243]);
        Ids(await client.GetFromJsonAsync<JsonElement>("/api/library?shelf=watchlist", Ct)).ShouldBeEmpty();

        var page2 = await client.GetFromJsonAsync<JsonElement>("/api/library?shelf=liked&sort=title&page=2&pageSize=2", Ct);
        Ids(page2).ShouldBe([496243]);
        page2.GetProperty("totalCount").GetInt32().ShouldBe(3);
        page2.GetProperty("items")[0].GetProperty("rating").GetInt32().ShouldBe(5, "the Liked shelf shows the rating");
    }

    [Theory]
    [InlineData("/api/library?shelf=nope")]
    [InlineData("/api/library?pageSize=0")]
    public async Task List_BadQuery_Is400(string url)
    {
        var client = await BrowserClient.RegisteredAsync(_factory, Ct);

        using var response = await client.GetAsync(url, Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task EachUserSeesOnlyTheirOwnLibrary()
    {
        var ada = await BrowserClient.RegisteredAsync(_factory, Ct);
        var bob = await BrowserClient.RegisteredAsync(_factory, Ct);
        (await PutAsync(ada, 550, liked: true)).Dispose();

        (await bob.GetFromJsonAsync<JsonElement>("/api/library/summary", Ct)).GetProperty("liked").GetInt32().ShouldBe(0);
        (await bob.GetFromJsonAsync<JsonElement>("/api/library/550", Ct)).GetProperty("liked").GetBoolean().ShouldBeFalse();
    }

    [Theory]
    [InlineData("GET", "/api/library")]
    [InlineData("GET", "/api/library/summary")]
    [InlineData("GET", "/api/library/550")]
    [InlineData("PUT", "/api/library/550")]
    [InlineData("PUT", "/api/library/550/watchlist")]
    [InlineData("DELETE", "/api/library/550/watchlist")]
    public async Task Anonymous_Is401(string method, string url)
    {
        var client = await BrowserClient.StartAsync(_factory, Ct);
        using var request = new HttpRequestMessage(new HttpMethod(method), url);
        if (method == "PUT" && !url.EndsWith("/watchlist", StringComparison.Ordinal))
        {
            request.Content = JsonContent.Create(new { liked = true, onWatchlist = false, seen = false, rating = (int?)null });
        }

        using var response = await client.SendAsync(request, Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await JsonAsync(response)).GetProperty("code").GetString().ShouldBe("auth.unauthenticated");
    }
}
