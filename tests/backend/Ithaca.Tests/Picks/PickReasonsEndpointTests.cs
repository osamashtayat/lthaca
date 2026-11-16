using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using Ithaca.Application.Ai;
using Ithaca.Application.Common;
using Ithaca.Application.Realtime;
using Ithaca.Domain.Films;
using Ithaca.Infrastructure.Persistence;
using Ithaca.Tests.Harness;
using Ithaca.Tests.Rooms;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Ithaca.Tests.Picks;

/// <summary>
/// BE-13: AI reasons on the solo top 3 and on a room's picks, through the real API with an LLM scripted per prompt that
/// records every request. The catalog gets films 793000–793009 in a genre of their own, so a mood deck (or a mood room)
/// that the scripted mood-parse points at that genre deals exactly them. Their TMDB fields carry markers that must never
/// reach a prompt; 793008 and 793009 have no Wikidata facts.
/// </summary>
[Collection(DatabaseCollectionDefinition.Name)]
public sealed class PickReasonsEndpointTests(PostgresFixture postgres) : IAsyncLifetime
{
    private const int First = 793000;
    private const int Count = 10;
    private const int WithoutFacts = 2;
    private const int Invented = 999_999;
    private const string Genre = "Pick Reason Test";
    private const string TitleMark = "TMDB-TITLE-MARK";
    private const string OverviewMark = "TMDB-OVERVIEW-MARK";
    private const string PosterMark = "/tmdb-poster-mark.jpg";

    private readonly PromptScriptedLlm _llm = new();
    private IthacaApiFactory _base = null!;
    private WebApplicationFactory<Program> _factory = null!;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static IEnumerable<int> FilmsWithFacts => Enumerable.Range(First, Count - WithoutFacts);

    public async ValueTask InitializeAsync()
    {
        _base = await IthacaApiFactory.StartAsync(postgres.ConnectionString);
        _factory = _base.WithWebHostBuilder(b => b.ConfigureTestServices(s =>
        {
            s.AddSingleton<ILlmClient>(_llm);
            s.AddSingleton<IRoomEventPublisher>(new RoomSwipingEndpointTests.RecordingPublisher());
        }));
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        if (await db.Films.AnyAsync(f => f.TmdbId == First, Ct))
        {
            return;
        }

        var now = DateTimeOffset.UtcNow;
        for (var i = 0; i < Count; i++)
        {
            var film = Film.Create(First + i, $"Reason film {i} {TitleMark}", now);
            film.UpdateDetails($"Reason film {i} {TitleMark}", 1990 + i, $"A plot {OverviewMark}", PosterMark, 100 + i, [Genre], 1000 - i, null, 7, 100, "en", now);
            db.Films.Add(film);
            if (i < Count - WithoutFacts)
            {
                db.FilmFacts.Add(FilmFacts.Create(
                    film.Id,
                    new WikidataFacts($"Q{9_000_000 + i}", ["neo-noir"], ["friendship"], [$"Director {i}"], [$"Actor {i}"], ["France"], ["French"], 1990 + i, 100 + i, ["Golden Bear"]),
                    now));
            }
        }

        await db.SaveChangesAsync(Ct);
    }

    public async ValueTask DisposeAsync()
    {
        await _factory.DisposeAsync();
        await _base.DisposeAsync();
    }

    [Fact]
    public async Task SoloTop_IsTheAiOrderWithReasons_DroppingIdsWeDidNotSend()
    {
        using var client = await BrowserClient.RegisteredAsync(_factory, Ct);
        var (deckId, cards) = await MoodDeckAsync(client);
        foreach (var card in cards)
        {
            await SwipeOkAsync(client, deckId, card.Id, "right");
        }

        // The model puts an invented film first, repeats one, and otherwise reverses what it got.
        _llm.Script("rank-candidates", request =>
        {
            var sent = Ids(request, "candidates");
            return Reply("ranked", [Invented, .. sent.AsEnumerable().Reverse().Prepend(sent[^1])], id => $"Reason for {id}.");
        });

        var top = await client.GetFromJsonAsync<JsonElement>($"/api/decks/{deckId}/top", Ct);

        var sentIds = Ids(_llm.Requests("rank-candidates").ShouldHaveSingleItem(), "candidates");
        sentIds.ShouldBeSubsetOf(FilmsWithFacts, "only films with Wikidata facts go to the model");
        sentIds.Count.ShouldBe(Count - WithoutFacts);
        var picks = top.GetProperty("picks").EnumerateArray().ToList();
        picks.Select(TmdbId).ShouldBe(sentIds.AsEnumerable().Reverse().Take(3), "the model's order, without the invented id or the repeat");
        picks.Select(p => p.GetProperty("reason").GetString()).ShouldBe(picks.Select(p => $"Reason for {TmdbId(p)}."));
    }

    [Fact]
    public async Task SoloTop_FillsUpInOurOrder_WhenTheAiPicksFewer()
    {
        using var client = await BrowserClient.RegisteredAsync(_factory, Ct);
        var (deckId, cards) = await MoodDeckAsync(client);
        foreach (var card in cards)
        {
            await SwipeOkAsync(client, deckId, card.Id, "right");
        }

        _llm.Fail("rank-candidates", "unavailable");
        var ours = (await client.GetFromJsonAsync<JsonElement>($"/api/decks/{deckId}/top", Ct)).GetProperty("picks").EnumerateArray().Select(TmdbId).ToList();
        var lastOfOurs = FilmsWithFacts.Last(id => !ours.Contains(id));
        _llm.Script("rank-candidates", _ => Reply("ranked", [lastOfOurs], _ => "Just this one."));

        var picks = (await client.GetFromJsonAsync<JsonElement>($"/api/decks/{deckId}/top", Ct)).GetProperty("picks").EnumerateArray().ToList();

        picks.Select(TmdbId).ShouldBe([lastOfOurs, .. ours.Take(2)]);
        picks.Select(p => p.GetProperty("reason").GetString()).ShouldBe(["Just this one.", null, null]);
    }

    [Theory]
    [InlineData("unavailable")]
    [InlineData("refused")]
    [InlineData("garbage")]
    public async Task SoloTop_WhenTheAiFails_IsOurTop3WithoutReasons(string failure)
    {
        using var client = await BrowserClient.RegisteredAsync(_factory, Ct);
        var (deckId, cards) = await MoodDeckAsync(client);
        foreach (var card in cards.Take(5))
        {
            await SwipeOkAsync(client, deckId, card.Id, "right");
        }

        _llm.Fail("rank-candidates", failure);
        using var response = await client.GetAsync(new Uri($"/api/decks/{deckId}/top", UriKind.Relative), Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var picks = (await response.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("picks").EnumerateArray().ToList();
        picks.Count.ShouldBe(3);
        picks.Select(TmdbId).ShouldBeSubsetOf(cards.Take(5).Select(c => c.TmdbId));
        picks.ShouldAllBe(p => p.GetProperty("reason").ValueKind == JsonValueKind.Null);
    }

    [Fact]
    public async Task SoloTop_PromptGetsWikidataFactsAndOurSignals_NeverTmdbText()
    {
        using var client = await BrowserClient.RegisteredAsync(_factory, Ct);
        var (deckId, cards) = await MoodDeckAsync(client);
        var away = cards.First(c => FilmsWithFacts.Contains(c.TmdbId));
        await SwipeOkAsync(client, deckId, away.Id, "left");
        foreach (var card in cards.Where(c => c != away))
        {
            await SwipeOkAsync(client, deckId, card.Id, "right");
        }

        (await client.GetAsync(new Uri($"/api/decks/{deckId}/top", UriKind.Relative), Ct)).Dispose();

        var request = _llm.Requests("rank-candidates").ShouldHaveSingleItem();
        request.Prompt.Id.ShouldBe(Prompts.RankCandidates);
        request.Prompt.Text.ShouldNotContain("{{");
        request.Prompt.Text.ShouldContain("one person");
        request.Prompt.Text.ShouldContain("at most 3 films");
        var input = JsonNode.Parse(request.Input)!;
        input["candidates"]![0]!["directors"]![0]!.GetValue<string>().ShouldStartWith("Director ");
        input["candidates"]![0]!["genres"]![0]!.GetValue<string>().ShouldBe("neo-noir");
        input["taste"]!["moodGenres"]!.AsArray().Select(g => g!.GetValue<string>()).ShouldBe([Genre]);
        input["taste"]!["dislikedGenres"]!.AsArray().Select(g => g!.GetValue<string>()).ShouldBe(["neo-noir"], "the left swipe's Wikidata genre");
        ShouldHoldNoTmdbText(request);
    }

    [Fact]
    public async Task RoomMatch_GetsReasonsOnce_ForEveryMember_FromCountsAndFactsOnly()
    {
        var (code, host, guest) = await MoodRoomAsync();
        using var _ = host.Client;
        using var __ = guest.Client;
        var card = (await RoomDeckAsync(host.Client, code)).First(c => FilmsWithFacts.Contains(c.TmdbId));
        _llm.Script("explain-pick", request => Reply("reasons", [Invented, .. Ids(request, "picks")], id => $"All of you said yes to {id}."));

        await VoteOkAsync(host.Client, code, card.Id, "right");
        await VoteOkAsync(guest.Client, code, card.Id, "right");
        var forGuest = await RoomResultAsync(guest.Client, code);
        var forHost = await RoomResultAsync(host.Client, code);

        forGuest.GetProperty("kind").GetString().ShouldBe("match");
        var pick = forGuest.GetProperty("picks").EnumerateArray().ShouldHaveSingleItem();
        pick.GetProperty("reason").GetString().ShouldBe($"All of you said yes to {card.TmdbId}.");
        forHost.GetRawText().ShouldBe(forGuest.GetRawText(), "everyone sees the same stored reasons");
        var request = _llm.Requests("explain-pick").ShouldHaveSingleItem("one AI call per room");
        request.Prompt.Id.ShouldBe(Prompts.ExplainPick);
        request.Prompt.Text.ShouldContain("a group of 2 friends");
        var input = JsonNode.Parse(request.Input)!;
        input["result"]!.GetValue<string>().ShouldBe("match");
        input["picks"]![0]!["yesCount"]!.GetValue<int>().ShouldBe(2);
        request.Input.ShouldNotContain(host.Id.ToString());
        request.Input.ShouldNotContain(guest.Id.ToString());
        request.Input.ShouldNotContain("Ada");
        request.Input.ShouldNotContain("Guest");
        ShouldHoldNoTmdbText(request);
    }

    [Fact]
    public async Task RoomCompromise_KeepsTheVoteOrder_AndRetriesTheReasonsAfterAFailure()
    {
        var (code, host, guest) = await MoodRoomAsync();
        using var _ = host.Client;
        using var __ = guest.Client;
        var deck = await RoomDeckAsync(host.Client, code);
        var yes = new[] { deck.First(c => c.TmdbId == First + 5), deck.First(c => c.TmdbId == First + 2), deck.First(c => c.TmdbId == First + Count - 1) };
        foreach (var card in yes)
        {
            await VoteOkAsync(host.Client, code, card.Id, "right");
        }

        _llm.Fail("explain-pick", "unavailable");
        using var finish = await host.Client.PostAsync(new Uri($"/api/rooms/{code}/finish", UriKind.Relative), null, Ct);
        finish.StatusCode.ShouldBe(HttpStatusCode.OK, await finish.Content.ReadAsStringAsync(Ct));
        var failed = await finish.Content.ReadFromJsonAsync<JsonElement>(Ct);
        failed.GetProperty("kind").GetString().ShouldBe("compromise");
        var expectedOrder = new[] { First + 2, First + 5, First + Count - 1 }; // same votes: more popular first (§6)
        failed.GetProperty("picks").EnumerateArray().Select(TmdbId).ShouldBe(expectedOrder);
        failed.GetProperty("picks").EnumerateArray().ShouldAllBe(p => p.GetProperty("reason").ValueKind == JsonValueKind.Null);
        (await StoredReasonsAsync(code)).ShouldBeNull("a failure stores nothing, so a later read tries again");

        _llm.Script("explain-pick", request => Reply("reasons", Ids(request, "picks").AsEnumerable().Reverse().ToList(), id => $"Reason {id}."));
        var result = await RoomResultAsync(guest.Client, code);

        var picks = result.GetProperty("picks").EnumerateArray().ToList();
        picks.Select(TmdbId).ShouldBe(expectedOrder, "the AI never reorders a room's picks");
        picks.Select(p => p.GetProperty("reason").GetString()).ShouldBe([$"Reason {First + 2}.", $"Reason {First + 5}.", null], "no facts, no reason");
        Ids(_llm.Requests("explain-pick").Last(), "picks").ShouldBe([First + 2, First + 5], "the film without facts isn't sent");
        (await StoredReasonsAsync(code)).ShouldBe([$"Reason {First + 2}.", $"Reason {First + 5}.", null]);
        (await RoomResultAsync(host.Client, code)).GetRawText().ShouldBe(result.GetRawText());
        _llm.Requests("explain-pick").Count.ShouldBe(2, "the failed try and the stored one");
    }

    private static void ShouldHoldNoTmdbText(LlmRequest request)
    {
        foreach (var mark in new[] { TitleMark, OverviewMark, PosterMark, "Reason film" })
        {
            request.Prompt.Text.ShouldNotContain(mark);
            request.Input.ShouldNotContain(mark);
        }
    }

    /// <summary>A mood deck the scripted mood-parse points at <see cref="Genre"/>: exactly films 793000–793009.</summary>
    private async Task<(string DeckId, List<Card> Cards)> MoodDeckAsync(HttpClient client)
    {
        _llm.Script("mood-parse", _ => MoodReply());
        using var response = await client.PostAsJsonAsync(
            "/api/decks", new { source = "mood", mood = new { moods = Array.Empty<string>(), maxRuntime = "any", language = (string?)null, text = "a night in" } }, Ct);
        response.StatusCode.ShouldBe(HttpStatusCode.Created, await response.Content.ReadAsStringAsync(Ct));
        var deck = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
        var cards = deck.GetProperty("items").EnumerateArray().Select(i => new Card(i.GetProperty("id").GetGuid(), TmdbId(i))).ToList();
        cards.Select(c => c.TmdbId).Order().ShouldBe(Enumerable.Range(First, Count));
        return (deck.GetProperty("id").GetString()!, cards);
    }

    private static async Task SwipeOkAsync(HttpClient client, string deckId, Guid itemId, string direction)
    {
        using var response = await client.PostAsJsonAsync(
            $"/api/decks/{deckId}/swipes", new { swipeId = Guid.NewGuid(), deckItemId = itemId, direction, seenVerdict = (string?)null }, Ct);
        response.StatusCode.ShouldBe(HttpStatusCode.NoContent, await response.Content.ReadAsStringAsync(Ct));
    }

    /// <summary>A started mood room (host + one guest) whose deck is exactly films 793000–793009.</summary>
    private async Task<(string Code, Member Host, Member Guest)> MoodRoomAsync()
    {
        _llm.Script("mood-parse", _ => MoodReply());
        var host = await BrowserClient.RegisteredAsync(_factory, Ct, "Ada");
        var created = await RoomsEndpointTests.CreateAsync(host, mode: "mood");
        var code = RoomsEndpointTests.Code(created);
        var guestClient = await BrowserClient.StartAsync(_factory, Ct);
        using var join = await guestClient.PostAsJsonAsync($"/api/rooms/{code}/join", new { displayName = "Guest Bea" }, Ct);
        join.StatusCode.ShouldBe(HttpStatusCode.OK);
        var guestId = (await join.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("me").GetProperty("id").GetGuid();
        using var mood = await host.PutAsJsonAsync(
            $"/api/rooms/{code}/mood", new { moods = Array.Empty<string>(), maxRuntime = "any", hardNos = Array.Empty<string>(), text = "a night in" }, Ct);
        mood.StatusCode.ShouldBe(HttpStatusCode.OK, await mood.Content.ReadAsStringAsync(Ct));
        using var start = await host.PostAsync(new Uri($"/api/rooms/{code}/start", UriKind.Relative), null, Ct);
        start.StatusCode.ShouldBe(HttpStatusCode.OK, await start.Content.ReadAsStringAsync(Ct));
        (await RoomDeckAsync(host, code)).Select(c => c.TmdbId).Order().ShouldBe(Enumerable.Range(First, Count));
        return (code, new Member(host, created.GetProperty("me").GetProperty("id").GetGuid()), new Member(guestClient, guestId));
    }

    private static async Task<List<Card>> RoomDeckAsync(HttpClient client, string code) =>
        [.. (await client.GetFromJsonAsync<JsonElement>($"/api/rooms/{code}/deck?fromPosition=0", Ct)).EnumerateArray().Select(i => new Card(i.GetProperty("id").GetGuid(), TmdbId(i)))];

    private static async Task VoteOkAsync(HttpClient client, string code, Guid deckItemId, string direction)
    {
        using var vote = await client.PostAsJsonAsync($"/api/rooms/{code}/votes", new { voteId = Guid.NewGuid(), deckItemId, direction, seenVerdict = (string?)null }, Ct);
        vote.StatusCode.ShouldBe(HttpStatusCode.NoContent, await vote.Content.ReadAsStringAsync(Ct));
    }

    private static async Task<JsonElement> RoomResultAsync(HttpClient client, string code)
    {
        using var response = await client.GetAsync(new Uri($"/api/rooms/{code}/result", UriKind.Relative), Ct);
        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync(Ct));
        return await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
    }

    private async Task<string?[]?> StoredReasonsAsync(string code)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.Rooms.AsNoTracking().Where(r => r.Code == code).Select(r => r.ResultReasons).SingleAsync(Ct);
    }

    private static int TmdbId(JsonElement withFilm) => withFilm.GetProperty("film").GetProperty("tmdbId").GetInt32();

    /// <summary>The TMDB ids in the request's input under <paramref name="list"/>, in order.</summary>
    private static List<int> Ids(LlmRequest request, string list) =>
        [.. JsonNode.Parse(request.Input)![list]!.AsArray().Select(c => c!["tmdbId"]!.GetValue<int>())];

    private static string Reply(string key, IReadOnlyList<int> ids, Func<int, string> reason) =>
        new JsonObject { [key] = new JsonArray([.. ids.Select(id => (JsonNode)new JsonObject { ["tmdbId"] = id, ["reason"] = reason(id) })]) }.ToJsonString();

    private static string MoodReply() =>
        $$"""{"intent":"mood","includeGenres":["{{Genre}}"],"excludeGenres":[],"maxRuntimeMinutes":null,"minReleaseYear":null,"maxReleaseYear":null,"keywords":["rainy"]}""";

    private sealed record Card(Guid Id, int TmdbId);

    private sealed record Member(HttpClient Client, Guid Id);

    /// <summary>Replies per prompt name (scripted JSON or a failure) and records every request.</summary>
    private sealed class PromptScriptedLlm : ILlmClient
    {
        private readonly ConcurrentDictionary<string, Func<LlmRequest, Result<LlmResponse>>> _replies = new(StringComparer.Ordinal);
        private readonly ConcurrentQueue<LlmRequest> _requests = new();

        public void Script(string prompt, Func<LlmRequest, string> json) =>
            _replies[prompt] = request => new LlmResponse(json(request), request.Prompt.Id, "scripted");

        public void Fail(string prompt, string failure) =>
            _replies[prompt] = request => failure switch
            {
                "refused" => AiErrors.Refused,
                "garbage" => new LlmResponse("not json", request.Prompt.Id, "scripted"),
                _ => AiErrors.Unavailable,
            };

        public List<LlmRequest> Requests(string prompt) => [.. _requests.Where(r => r.Prompt.Id.Name == prompt)];

        public Task<Result<LlmResponse>> CompleteAsync(LlmRequest request, CancellationToken ct)
        {
            _requests.Enqueue(request);
            return Task.FromResult(_replies.TryGetValue(request.Prompt.Id.Name, out var reply)
                ? reply(request)
                : AiErrors.Unavailable);
        }
    }
}
