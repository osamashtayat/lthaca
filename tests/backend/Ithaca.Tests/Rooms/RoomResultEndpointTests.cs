using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Ithaca.Application.Ai;
using Ithaca.Application.Realtime;
using Ithaca.Domain.Decks;
using Ithaca.Domain.Films;
using Ithaca.Domain.Rooms;
using Ithaca.Infrastructure.Persistence;
using Ithaca.Tests.Harness;
using Ithaca.Tests.Moods;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Ithaca.Tests.Rooms;

/// <summary>
/// BE-12: a room's result through the real API (api-contract.md §6): the match on a vote, the compromise once everyone
/// finished, the host's finish, deciding when someone leaves, hard nos, and exactly one result under concurrency. The
/// catalog gets films 792xxx, among them a hugely popular horror film that only a hard no keeps out.
/// </summary>
[Collection(DatabaseCollectionDefinition.Name)]
public sealed class RoomResultEndpointTests(PostgresFixture postgres) : IAsyncLifetime
{
    private const int PopularHorror = 792000;

    private static readonly string[] SurpriseMood = ["surprise"];

    private readonly MoodDecksEndpointTests.ScriptedLlm _llm = new();
    private readonly RoomSwipingEndpointTests.RecordingPublisher _events = new();
    private IthacaApiFactory _base = null!;
    private WebApplicationFactory<Program> _factory = null!;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync()
    {
        _base = await IthacaApiFactory.StartAsync(postgres.ConnectionString);
        _factory = _base.WithWebHostBuilder(b => b.ConfigureTestServices(s =>
        {
            s.AddSingleton<ILlmClient>(_llm);
            s.AddSingleton<IRoomEventPublisher>(_events);
        }));
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        if (await db.Films.AnyAsync(f => f.TmdbId == PopularHorror, Ct))
        {
            return;
        }

        var now = DateTimeOffset.UtcNow;
        var horror = Film.Create(PopularHorror, "Popular horror", now);
        horror.UpdateDetails("Popular horror", 2010, "Overview", null, 95, ["Horror"], 10_000_000, null, 7, 100, "en", now);
        db.Films.Add(horror);
        string[] plain = ["Comedy", "Adventure", "Family", "Mystery", "Romance", "Science Fiction"];
        for (var i = 1; i <= 40; i++)
        {
            var film = Film.Create(PopularHorror + i, $"Result film {i}", now);
            film.UpdateDetails($"Result film {i}", 1980 + i, "Overview", null, 85 + i, [plain[i % plain.Length]], 40_000 - i, null, 7, 100, "en", now);
            db.Films.Add(film);
        }

        await db.SaveChangesAsync(Ct);
    }

    public async ValueTask DisposeAsync()
    {
        await _factory.DisposeAsync();
        await _base.DisposeAsync();
    }

    [Fact]
    public async Task Match_WhenEveryoneSwipesRightOnOneCard_IsTheResult_AndEndsSwiping()
    {
        var room = await StartedRoomAsync(guests: 1);
        using var _ = room;
        var (host, guest) = (room.Members[0], room.Members[1]);
        var deck = await DeckAsync(host.Client, room.Code);
        var card = deck[1];

        await VoteOkAsync(host.Client, room.Code, card.Id, "right");
        await VoteOkAsync(guest.Client, room.Code, deck[0].Id, "left");
        _events.Of(room.Id, RoomEvents.RoomMatched).ShouldBeEmpty("the guest hasn't said yes to it yet");
        await (await GetAsync(guest.Client, $"/api/rooms/{room.Code}/result")).ShouldBeProblem(HttpStatusCode.Conflict, "room.no_result");

        await VoteOkAsync(guest.Client, room.Code, card.Id, "right");

        var result = await ResultAsync(guest.Client, room.Code);
        result.GetProperty("kind").GetString().ShouldBe("match");
        result.GetProperty("memberCount").GetInt32().ShouldBe(2);
        var pick = result.GetProperty("picks").EnumerateArray().ShouldHaveSingleItem();
        pick.GetProperty("rank").GetInt32().ShouldBe(1);
        pick.GetProperty("film").GetProperty("tmdbId").GetInt32().ShouldBe(card.TmdbId);
        pick.GetProperty("yesCount").GetInt32().ShouldBe(2);
        pick.GetProperty("noCount").GetInt32().ShouldBe(0);
        Ids(pick, "yes").ShouldBe([host.Id, guest.Id]);
        Ids(pick, "seen").ShouldBeEmpty();
        JsonSerializer.Serialize(_events.Of(room.Id, RoomEvents.RoomMatched).ShouldHaveSingleItem())
            .ShouldBe(JsonSerializer.Serialize(new RoomMatchedEvent(room.Id, card.TmdbId)));
        (await RoomAsync(host.Client, room.Code)).GetProperty("status").GetString().ShouldBe("result");

        await (await VoteAsync(host.Client, room.Code, deck[2].Id, "right")).ShouldBeProblem(HttpStatusCode.Conflict, "room.invalid_state");
        using var finish = await host.Client.PostAsync(new Uri($"/api/rooms/{room.Code}/finish", UriKind.Relative), null, Ct);
        finish.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await finish.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetRawText().ShouldBe(result.GetRawText(), "finishing a decided room returns its result");
        _events.Of(room.Id, RoomEvents.RoomMatched).Count.ShouldBe(1);

        (await host.Client.PostAsync(new Uri($"/api/rooms/{room.Code}/close", UriKind.Relative), null, Ct)).Dispose();
        (await ResultAsync(guest.Client, room.Code)).GetRawText().ShouldBe(result.GetRawText(), "the result outlives closing");
        await (await PostAsync(host.Client, $"/api/rooms/{room.Code}/finish")).ShouldBeProblem(HttpStatusCode.Conflict, "room.invalid_state");
    }

    [Fact]
    public async Task Result_IgnoresAVoteCommittedAfterTheDecision()
    {
        var room = await StartedRoomAsync(guests: 2);
        using var _ = room;
        var card = (await DeckAsync(room.Members[0].Client, room.Code))[0];
        await VoteOkAsync(room.Members[0].Client, room.Code, card.Id, "right");
        await VoteOkAsync(room.Members[1].Client, room.Code, card.Id, "right");
        (await PostAsync(room.Members[0].Client, $"/api/rooms/{room.Code}/finish")).Dispose();
        var before = await ResultAsync(room.Members[0].Client, room.Code);

        // A vote that passed its "still swiping" check before the finish but commits after it.
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Votes.Add(Vote.Create(Guid.NewGuid(), card.Id, room.Members[2].Id, SwipeDirection.Left, null, DateTimeOffset.UtcNow));
            await db.SaveChangesAsync(Ct);
        }

        var after = await ResultAsync(room.Members[1].Client, room.Code);
        after.GetRawText().ShouldBe(before.GetRawText());
        after.GetProperty("picks")[0].GetProperty("noCount").GetInt32().ShouldBe(0);
    }

    [Fact]
    public async Task RetriedLastVote_DecidesTheRoom_WhenTheFirstTryStoredItButDidNotDecide()
    {
        var room = await StartedRoomAsync(guests: 1);
        using var _ = room;
        var card = (await DeckAsync(room.Members[0].Client, room.Code))[0];
        await VoteOkAsync(room.Members[0].Client, room.Code, card.Id, "right");
        var voteId = Guid.NewGuid();

        // The first try stored the guest's vote and then failed before deciding (simulated: written straight to the database).
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Votes.Add(Vote.Create(voteId, card.Id, room.Members[1].Id, SwipeDirection.Right, null, DateTimeOffset.UtcNow));
            await db.SaveChangesAsync(Ct);
        }

        using var retry = await room.Members[1].Client.PostAsJsonAsync(
            $"/api/rooms/{room.Code}/votes", new { voteId, deckItemId = card.Id, direction = "right", seenVerdict = (string?)null }, Ct);

        retry.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await ResultAsync(room.Members[0].Client, room.Code)).GetProperty("kind").GetString().ShouldBe("match");
        _events.Of(room.Id, RoomEvents.RoomMatched).Count.ShouldBe(1);
    }

    [Fact]
    public async Task Compromise_OnceEveryoneFinishedTheDeck_RanksYes_ThenFewerNo_AndOnlyCountsTheNoes()
    {
        var room = await StartedRoomAsync(guests: 1);
        using var _ = room;
        var (host, guest) = (room.Members[0], room.Members[1]);
        int[] seenPositions = [3, 7];

        await VoteAllAsync(host.Client, room.Code, _ => "right");
        _events.Of(room.Id, RoomEvents.RoomMatched).ShouldBeEmpty();
        await VoteAllAsync(guest.Client, room.Code, position => seenPositions.Contains(position) ? "up" : "left");

        var deck = await DeckAsync(host.Client, room.Code);
        deck.Count.ShouldBe(Deck.RoomMaxItems, "the deck grew to its cap before anyone could finish");
        var result = await ResultAsync(host.Client, room.Code);
        result.GetProperty("kind").GetString().ShouldBe("compromise");
        var picks = result.GetProperty("picks").EnumerateArray().ToList();
        picks.Count.ShouldBe(3);
        picks.Select(p => p.GetProperty("rank").GetInt32()).ShouldBe([1, 2, 3]);

        var seenTmdbIds = deck.Where(c => seenPositions.Contains(c.Position)).Select(c => c.TmdbId).ToList();
        picks.Take(2).Select(TmdbId).ShouldBe(seenTmdbIds, ignoreOrder: true, "one yes and no noes beats one yes and one no");
        foreach (var pick in picks.Take(2))
        {
            Ids(pick, "yes").ShouldBe([host.Id]);
            Ids(pick, "seen").ShouldBe([guest.Id]);
            pick.GetProperty("noCount").GetInt32().ShouldBe(0);
        }

        picks[2].GetProperty("noCount").GetInt32().ShouldBe(1);
        picks[2].GetRawText().ShouldNotContain(guest.Id.ToString(), Case.Insensitive, "who said no is never revealed (A7)");
        JsonSerializer.Serialize(_events.Of(room.Id, RoomEvents.RoomMatched).ShouldHaveSingleItem())
            .ShouldBe(JsonSerializer.Serialize(new RoomMatchedEvent(room.Id, TmdbId(picks[0]))));
    }

    [Fact]
    public async Task Finish_ByTheHost_IsTheCompromiseOfTheYesVotesSoFar_AndFollowsTheContract()
    {
        var host = await BrowserClient.RegisteredAsync(_factory, Ct, "Ada");
        using var _h = host;
        var code = RoomsEndpointTests.Code(await RoomsEndpointTests.CreateAsync(host));
        var guest = await JoinAsync(code, "Omar");
        using var _g = guest.Client;
        await (await PostAsync(host, $"/api/rooms/{code}/finish")).ShouldBeProblem(HttpStatusCode.Conflict, "room.invalid_state");
        (await PostAsync(host, $"/api/rooms/{code}/start")).Dispose();
        await (await PostAsync(guest.Client, $"/api/rooms/{code}/finish")).ShouldBeProblem(HttpStatusCode.Forbidden, "room.not_host");
        var stranger = await BrowserClient.RegisteredAsync(_factory, Ct, "Zed");
        using var _s = stranger;
        await (await GetAsync(stranger, $"/api/rooms/{code}/result")).ShouldBeProblem(HttpStatusCode.Forbidden, "room.not_member");
        var deck = await DeckAsync(host, code);
        await VoteOkAsync(host, code, deck[0].Id, "right");
        await VoteOkAsync(guest.Client, code, deck[0].Id, "left");
        await VoteOkAsync(guest.Client, code, deck[1].Id, "left");

        using var finish = await host.PostAsync(new Uri($"/api/rooms/{code}/finish", UriKind.Relative), null, Ct);

        finish.StatusCode.ShouldBe(HttpStatusCode.OK, await finish.Content.ReadAsStringAsync(Ct));
        var result = await finish.Content.ReadFromJsonAsync<JsonElement>(Ct);
        result.GetProperty("kind").GetString().ShouldBe("compromise");
        var pick = result.GetProperty("picks").EnumerateArray().ShouldHaveSingleItem("only cards someone said yes to");
        TmdbId(pick).ShouldBe(deck[0].TmdbId);
        pick.GetProperty("noCount").GetInt32().ShouldBe(1);
        (await ResultAsync(guest.Client, code)).GetRawText().ShouldBe(result.GetRawText());
    }

    [Fact]
    public async Task Finish_BeforeAnyYes_IsAnEmptyCompromise_AnnouncedWithoutAFilm()
    {
        var room = await StartedRoomAsync(guests: 1);
        using var _ = room;

        using var finish = await room.Members[0].Client.PostAsync(new Uri($"/api/rooms/{room.Code}/finish", UriKind.Relative), null, Ct);

        finish.StatusCode.ShouldBe(HttpStatusCode.OK);
        var result = await finish.Content.ReadFromJsonAsync<JsonElement>(Ct);
        result.GetProperty("kind").GetString().ShouldBe("compromise");
        result.GetProperty("picks").GetArrayLength().ShouldBe(0);
        JsonSerializer.Serialize(_events.Of(room.Id, RoomEvents.RoomMatched).ShouldHaveSingleItem())
            .ShouldBe(JsonSerializer.Serialize(new RoomMatchedEvent(room.Id, null)));
    }

    [Fact]
    public async Task Leave_DecidesTheRoom_WhenEveryoneStillThereAgrees_AndTheResultCountsOnlyThem()
    {
        var room = await StartedRoomAsync(guests: 2);
        using var _ = room;
        var card = (await DeckAsync(room.Members[0].Client, room.Code))[0];
        await VoteOkAsync(room.Members[0].Client, room.Code, card.Id, "right");
        await VoteOkAsync(room.Members[1].Client, room.Code, card.Id, "right");
        _events.Of(room.Id, RoomEvents.RoomMatched).ShouldBeEmpty();

        (await PostAsync(room.Members[2].Client, $"/api/rooms/{room.Code}/leave")).StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var result = await ResultAsync(room.Members[0].Client, room.Code);
        result.GetProperty("kind").GetString().ShouldBe("match");
        result.GetProperty("memberCount").GetInt32().ShouldBe(2);
        Ids(result.GetProperty("picks")[0], "yes").ShouldBe([room.Members[0].Id, room.Members[1].Id]);
        _events.Of(room.Id, RoomEvents.RoomMatched).Count.ShouldBe(1);
    }

    [Fact]
    public async Task HardNos_NeverReachTheResult_EvenForTheMostPopularFilm()
    {
        var host = await BrowserClient.RegisteredAsync(_factory, Ct, "Ada");
        using var _h = host;
        var code = RoomsEndpointTests.Code(await RoomsEndpointTests.CreateAsync(host, mode: "mood"));
        var guest = await JoinAsync(code, "Omar");
        using var _g = guest.Client;
        (await PutMoodAsync(host, code, hardNos: [])).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await PutMoodAsync(guest.Client, code, hardNos: ["horror"])).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await PostAsync(host, $"/api/rooms/{code}/start")).StatusCode.ShouldBe(HttpStatusCode.OK);
        await VoteAllAsync(host, code, _ => "right");

        using var finish = await host.PostAsync(new Uri($"/api/rooms/{code}/finish", UriKind.Relative), null, Ct);

        var result = await finish.Content.ReadFromJsonAsync<JsonElement>(Ct);
        var picks = result.GetProperty("picks").EnumerateArray().ToList();
        picks.Count.ShouldBe(3);
        foreach (var film in picks.Select(p => p.GetProperty("film")))
        {
            film.GetProperty("tmdbId").GetInt32().ShouldNotBe(PopularHorror);
            film.GetProperty("genres").EnumerateArray().Select(g => g.GetString()).ShouldNotContain("Horror");
        }
    }

    [Fact]
    public async Task UnanimousLastVotes_AtTheSameMoment_DecideExactlyOneMatch_Over20Runs()
    {
        const int runs = 20;
        for (var run = 0; run < runs; run++)
        {
            var room = await StartedRoomAsync(guests: 3);
            using var _ = room;
            var deck = await DeckAsync(room.Members[0].Client, room.Code);
            var (first, second) = (deck[0].Id, deck[1].Id);

            // Odd runs: everyone's last vote lands at once on one card. Even runs: two members have said yes to both cards and
            // the other two say yes to the first and the second at once, so two matches race.
            List<Task<HttpResponseMessage>> calls;
            if (run % 2 == 1)
            {
                calls = [.. room.Members.Select(m => VoteAsync(m.Client, room.Code, first, "right"))];
            }
            else
            {
                foreach (var member in room.Members.Take(2))
                {
                    await VoteOkAsync(member.Client, room.Code, first, "right");
                    await VoteOkAsync(member.Client, room.Code, second, "right");
                }

                calls = [.. room.Members.Skip(2).SelectMany(m => new[]
                {
                    VoteAsync(m.Client, room.Code, first, "right"),
                    VoteAsync(m.Client, room.Code, second, "right"),
                })];
            }

            foreach (var response in await Task.WhenAll(calls))
            {
                response.StatusCode.ShouldBeOneOf([HttpStatusCode.NoContent, HttpStatusCode.Conflict], $"run {run}: {await response.Content.ReadAsStringAsync(Ct)}");
                response.Dispose();
            }

            _events.Of(room.Id, RoomEvents.RoomMatched).Count.ShouldBe(1, $"run {run}: exactly one result");
            var result = await ResultAsync(room.Members[3].Client, room.Code);
            result.GetProperty("kind").GetString().ShouldBe("match", $"run {run}");
            var pick = result.GetProperty("picks").EnumerateArray().ShouldHaveSingleItem();
            pick.GetProperty("yesCount").GetInt32().ShouldBe(4, $"run {run}");
            JsonSerializer.Serialize(_events.Of(room.Id, RoomEvents.RoomMatched)[0])
                .ShouldBe(JsonSerializer.Serialize(new RoomMatchedEvent(room.Id, TmdbId(pick))), $"run {run}");
        }
    }

    private async Task<StartedRoom> StartedRoomAsync(int guests)
    {
        var host = await BrowserClient.RegisteredAsync(_factory, Ct, "Ada");
        var created = await RoomsEndpointTests.CreateAsync(host);
        var code = RoomsEndpointTests.Code(created);
        List<Member> members = [new(host, created.GetProperty("me").GetProperty("id").GetGuid())];
        for (var g = 0; g < guests; g++)
        {
            members.Add(await JoinAsync(code, $"Guest {g}"));
        }

        using var start = await PostAsync(host, $"/api/rooms/{code}/start");
        start.StatusCode.ShouldBe(HttpStatusCode.OK, await start.Content.ReadAsStringAsync(Ct));
        return new StartedRoom((await start.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("id").GetGuid(), code, members);
    }

    private async Task<Member> JoinAsync(string code, string name)
    {
        var guest = await BrowserClient.StartAsync(_factory, Ct);
        using var join = await guest.PostAsJsonAsync($"/api/rooms/{code}/join", new { displayName = name }, Ct);
        join.StatusCode.ShouldBe(HttpStatusCode.OK);
        return new Member(guest, (await join.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("me").GetProperty("id").GetGuid());
    }

    /// <summary>The caller votes on every card (the deck grows as they go) with the direction for its position.</summary>
    private static async Task VoteAllAsync(HttpClient client, string code, Func<int, string> direction)
    {
        while ((await DeckAsync(client, code)).FirstOrDefault(c => !c.Voted) is { } next)
        {
            await VoteOkAsync(client, code, next.Id, direction(next.Position));
        }
    }

    private static async Task<List<Card>> DeckAsync(HttpClient client, string code)
    {
        var deck = await client.GetFromJsonAsync<JsonElement>($"/api/rooms/{code}/deck?fromPosition=0", Ct);
        return [.. deck.EnumerateArray().Select(i => new Card(
            i.GetProperty("id").GetGuid(), i.GetProperty("position").GetInt32(), TmdbId(i), i.GetProperty("swipe").ValueKind != JsonValueKind.Null))];
    }

    private static async Task VoteOkAsync(HttpClient client, string code, Guid deckItemId, string direction)
    {
        using var vote = await VoteAsync(client, code, deckItemId, direction);
        vote.StatusCode.ShouldBe(HttpStatusCode.NoContent, await vote.Content.ReadAsStringAsync(Ct));
    }

    private static Task<HttpResponseMessage> VoteAsync(HttpClient client, string code, Guid deckItemId, string direction) =>
        client.PostAsJsonAsync($"/api/rooms/{code}/votes", new { voteId = Guid.NewGuid(), deckItemId, direction, seenVerdict = (string?)null }, Ct);

    private static Task<HttpResponseMessage> PutMoodAsync(HttpClient client, string code, string[] hardNos) =>
        client.PutAsJsonAsync($"/api/rooms/{code}/mood", new { moods = SurpriseMood, maxRuntime = "any", hardNos, text = (string?)null }, Ct);

    private static Task<HttpResponseMessage> PostAsync(HttpClient client, string path) =>
        client.PostAsync(new Uri(path, UriKind.Relative), null, Ct);

    private static Task<HttpResponseMessage> GetAsync(HttpClient client, string path) =>
        client.GetAsync(new Uri(path, UriKind.Relative), Ct);

    private static async Task<JsonElement> ResultAsync(HttpClient client, string code)
    {
        using var response = await GetAsync(client, $"/api/rooms/{code}/result");
        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync(Ct));
        return await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
    }

    private static Task<JsonElement> RoomAsync(HttpClient client, string code) =>
        client.GetFromJsonAsync<JsonElement>($"/api/rooms/{code}", Ct);

    private static int TmdbId(JsonElement withFilm) => withFilm.GetProperty("film").GetProperty("tmdbId").GetInt32();

    private static List<Guid> Ids(JsonElement pick, string property) => [.. pick.GetProperty(property).EnumerateArray().Select(e => e.GetGuid())];

    private sealed record Member(HttpClient Client, Guid Id);

    private sealed record Card(Guid Id, int Position, int TmdbId, bool Voted);

    private sealed record StartedRoom(Guid Id, string Code, List<Member> Members) : IDisposable
    {
        public void Dispose()
        {
            foreach (var member in Members)
            {
                member.Client.Dispose();
            }
        }
    }
}

/// <summary>Problem-response assertions for these tests.</summary>
internal static class ProblemAssertions
{
    public static async Task ShouldBeProblem(this HttpResponseMessage response, HttpStatusCode status, string code)
    {
        using var _ = response;
        response.StatusCode.ShouldBe(status, await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        (await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken)).GetProperty("code").GetString().ShouldBe(code);
    }
}
