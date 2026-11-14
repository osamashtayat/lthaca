using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Ithaca.Tests.Harness;

namespace Ithaca.Tests.Rooms;

/// <summary>
/// BE-10: rooms core through the real API (api-contract.md §6: create, preview, join, get, leave, close), with users and
/// guests as browser clients (cookies + XSRF). Every test makes its own rooms.
/// </summary>
[Collection(DatabaseCollectionDefinition.Name)]
public sealed class RoomsEndpointTests(PostgresFixture postgres) : IAsyncLifetime
{
    private IthacaApiFactory _factory = null!;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync() => _factory = await IthacaApiFactory.StartAsync(postgres.ConnectionString);

    public ValueTask DisposeAsync() => _factory.DisposeAsync();

    [Fact]
    public async Task Create_AsUser_ReturnsLobbyHostedByTheCaller()
    {
        using var host = await BrowserClient.RegisteredAsync(_factory, Ct, "Ada");

        using var response = await host.PostAsJsonAsync("/api/rooms", new { name = "  Friday Film Club ", mode = "justSwipe", startsAt = (string?)null }, Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Created, await response.Content.ReadAsStringAsync(Ct));
        var room = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
        var code = room.GetProperty("code").GetString()!;
        code.ShouldMatch("^[A-Z]{3}-[0-9]{3}$");
        response.Headers.Location!.ToString().ShouldBe($"/api/rooms/{code}");
        room.GetProperty("name").GetString().ShouldBe("Friday Film Club");
        room.GetProperty("status").GetString().ShouldBe("lobby");
        room.GetProperty("mode").GetString().ShouldBe("justSwipe");
        room.GetProperty("maxMembers").GetInt32().ShouldBe(8);
        room.GetProperty("deckSize").GetInt32().ShouldBe(0);
        var me = room.GetProperty("me");
        me.GetProperty("displayName").GetString().ShouldBe("Ada");
        me.GetProperty("kind").GetString().ShouldBe("user");
        me.GetProperty("isHost").GetBoolean().ShouldBeTrue();
        me.GetProperty("isReady").GetBoolean().ShouldBeTrue();
        room.GetProperty("members").GetArrayLength().ShouldBe(1);
    }

    [Fact]
    public async Task Create_MoodRoom_HostIsNotReadyYet()
    {
        using var host = await BrowserClient.RegisteredAsync(_factory, Ct);

        var room = await CreateAsync(host, mode: "mood");

        room.GetProperty("me").GetProperty("isReady").GetBoolean().ShouldBeFalse();
    }

    [Fact]
    public async Task Create_BlankName_IsValidationError()
    {
        using var host = await BrowserClient.RegisteredAsync(_factory, Ct);

        using var response = await host.PostAsJsonAsync("/api/rooms", new { name = "   ", mode = "justSwipe", startsAt = (string?)null }, Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Create_AsAnonymousOrGuest_IsUnauthenticated()
    {
        using var host = await BrowserClient.RegisteredAsync(_factory, Ct);
        var code = Code(await CreateAsync(host));
        using var guest = await JoinAsGuestAsync(code, "Omar");
        using var anonymous = await BrowserClient.StartAsync(_factory, Ct);

        foreach (var client in new[] { anonymous, guest })
        {
            using var response = await client.PostAsJsonAsync("/api/rooms", new { name = "Mine", mode = "justSwipe", startsAt = (string?)null }, Ct);
            response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
            (await ProblemCodeAsync(response)).ShouldBe("auth.unauthenticated");
        }
    }

    [Fact]
    public async Task Preview_IsAnonymousAndCaseInsensitive()
    {
        using var host = await BrowserClient.RegisteredAsync(_factory, Ct, "Lina");
        var room = await CreateAsync(host, name: "Movie night");
        using var anonymous = _factory.CreateClient();

        var preview = await anonymous.GetFromJsonAsync<JsonElement>($"/api/rooms/{Code(room).ToLowerInvariant()}/preview", Ct);

        preview.GetProperty("code").GetString().ShouldBe(Code(room));
        preview.GetProperty("name").GetString().ShouldBe("Movie night");
        preview.GetProperty("hostDisplayName").GetString().ShouldBe("Lina");
        preview.GetProperty("status").GetString().ShouldBe("lobby");
        preview.GetProperty("memberCount").GetInt32().ShouldBe(1);
        preview.GetProperty("maxMembers").GetInt32().ShouldBe(8);
    }

    [Theory]
    [InlineData("ZZZ-999")]
    [InlineData("not-a-code")]
    public async Task UnknownCode_IsRoomNotFound(string code)
    {
        using var client = await BrowserClient.StartAsync(_factory, Ct);

        using var preview = await client.GetAsync($"/api/rooms/{code}/preview", Ct);
        using var join = await client.PostAsJsonAsync($"/api/rooms/{code}/join", new { displayName = "Omar" }, Ct);

        preview.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await ProblemCodeAsync(preview)).ShouldBe("room.not_found");
        join.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Join_Anonymous_BecomesAGuestScopedToThatRoom()
    {
        using var host = await BrowserClient.RegisteredAsync(_factory, Ct);
        var code = Code(await CreateAsync(host));
        using var guest = await BrowserClient.StartAsync(_factory, Ct);

        using var response = await guest.PostAsJsonAsync($"/api/rooms/{code}/join", new { displayName = " Omar " }, Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync(Ct));
        BrowserClient.SetCookie(response, "ithaca_session").ShouldNotBeNull();
        BrowserClient.SetCookie(response, "XSRF-TOKEN").ShouldNotBeNull();
        var room = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
        room.GetProperty("me").GetProperty("kind").GetString().ShouldBe("guest");
        room.GetProperty("me").GetProperty("displayName").GetString().ShouldBe("Omar");
        room.GetProperty("me").GetProperty("isHost").GetBoolean().ShouldBeFalse();
        room.GetProperty("members").GetArrayLength().ShouldBe(2);

        var me = await guest.GetFromJsonAsync<JsonElement>("/api/auth/me", Ct);
        me.GetProperty("kind").GetString().ShouldBe("guest");
        me.GetProperty("roomCode").GetString().ShouldBe(code);
        (await guest.GetAsync($"/api/rooms/{code}", Ct)).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Join_Anonymous_WithoutAName_IsValidationError()
    {
        using var host = await BrowserClient.RegisteredAsync(_factory, Ct);
        var code = Code(await CreateAsync(host));
        using var anonymous = await BrowserClient.StartAsync(_factory, Ct);

        using var response = await anonymous.PostAsJsonAsync($"/api/rooms/{code}/join", new { displayName = (string?)null }, Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        using var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct));
        problem.RootElement.GetProperty("errors").TryGetProperty("displayName", out _).ShouldBeTrue();
    }

    [Fact]
    public async Task Join_Twice_IsIdempotent()
    {
        using var host = await BrowserClient.RegisteredAsync(_factory, Ct);
        var code = Code(await CreateAsync(host));
        using var guest = await JoinAsGuestAsync(code, "Omar");
        using var user = await BrowserClient.RegisteredAsync(_factory, Ct, "Sam");
        var first = await JoinAsync(user, code);

        using var again = await guest.PostAsJsonAsync($"/api/rooms/{code}/join", new { displayName = "Someone else" }, Ct);
        var second = await JoinAsync(user, code, displayName: "ignored");

        again.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await again.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("me").GetProperty("displayName").GetString().ShouldBe("Omar");
        second.GetProperty("me").GetProperty("id").GetString().ShouldBe(first.GetProperty("me").GetProperty("id").GetString());
        second.GetProperty("me").GetProperty("displayName").GetString().ShouldBe("Sam");
        second.GetProperty("members").GetArrayLength().ShouldBe(3);
    }

    [Fact]
    public async Task GuestCookie_WorksOnlyForItsRoom()
    {
        using var host = await BrowserClient.RegisteredAsync(_factory, Ct);
        var codeA = Code(await CreateAsync(host));
        var codeB = Code(await CreateAsync(host));
        using var guest = await JoinAsGuestAsync(codeA, "Omar");

        using var other = await guest.GetAsync($"/api/rooms/{codeB}", Ct);
        using var leaveOther = await guest.PostAsync(new Uri($"/api/rooms/{codeB}/leave", UriKind.Relative), null, Ct);
        using var closeOther = await guest.PostAsync(new Uri($"/api/rooms/{codeB}/close", UriKind.Relative), null, Ct);

        other.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await ProblemCodeAsync(other)).ShouldBe("room.not_member");
        leaveOther.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        closeOther.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await guest.GetAsync($"/api/rooms/{codeA}", Ct)).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GuestJoiningAnotherRoom_GetsANewGuestForIt()
    {
        using var host = await BrowserClient.RegisteredAsync(_factory, Ct);
        var codeA = Code(await CreateAsync(host));
        var codeB = Code(await CreateAsync(host));
        using var guest = await JoinAsGuestAsync(codeA, "Omar");

        var roomB = await JoinAsync(guest, codeB, displayName: "Omar B");

        roomB.GetProperty("me").GetProperty("displayName").GetString().ShouldBe("Omar B");
        (await guest.GetFromJsonAsync<JsonElement>("/api/auth/me", Ct)).GetProperty("roomCode").GetString().ShouldBe(codeB);
        (await guest.GetAsync($"/api/rooms/{codeA}", Ct)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Get_Anonymous_IsUnauthenticated_AndUserNonMember_IsForbidden()
    {
        using var host = await BrowserClient.RegisteredAsync(_factory, Ct);
        var code = Code(await CreateAsync(host));
        using var anonymous = await BrowserClient.StartAsync(_factory, Ct);
        using var stranger = await BrowserClient.RegisteredAsync(_factory, Ct);

        using var anon = await anonymous.GetAsync($"/api/rooms/{code}", Ct);
        using var notMember = await stranger.GetAsync($"/api/rooms/{code}", Ct);

        anon.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        notMember.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await ProblemCodeAsync(notMember)).ShouldBe("room.not_member");
    }

    [Fact]
    public async Task Join_NinthMember_IsRoomFull()
    {
        using var host = await BrowserClient.RegisteredAsync(_factory, Ct);
        var code = Code(await CreateAsync(host));
        var guests = new List<HttpClient>();
        for (var i = 1; i < 8; i++)
        {
            guests.Add(await JoinAsGuestAsync(code, $"Guest {i}"));
        }

        using var ninth = await BrowserClient.StartAsync(_factory, Ct);
        using var response = await ninth.PostAsJsonAsync($"/api/rooms/{code}/join", new { displayName = "Late" }, Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await ProblemCodeAsync(response)).ShouldBe("room.full");

        // A seat frees up when someone leaves.
        (await guests[0].PostAsync(new Uri($"/api/rooms/{code}/leave", UriKind.Relative), null, Ct)).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await ninth.PostAsJsonAsync($"/api/rooms/{code}/join", new { displayName = "Late" }, Ct)).StatusCode.ShouldBe(HttpStatusCode.OK);
        guests.ForEach(g => g.Dispose());
    }

    [Fact]
    public async Task Join_ManyAtOnce_NeverMoreThanEight()
    {
        for (var run = 0; run < 3; run++)
        {
            using var host = await BrowserClient.RegisteredAsync(_factory, Ct);
            var code = Code(await CreateAsync(host));
            var clients = await Task.WhenAll(Enumerable.Range(0, 12).Select(_ => BrowserClient.StartAsync(_factory, Ct)));

            var responses = await Task.WhenAll(clients.Select((c, i) => c.PostAsJsonAsync($"/api/rooms/{code}/join", new { displayName = $"G{i}" }, Ct)));

            responses.Count(r => r.StatusCode == HttpStatusCode.OK).ShouldBe(7);
            responses.Count(r => r.StatusCode == HttpStatusCode.Conflict).ShouldBe(5);
            var room = await host.GetFromJsonAsync<JsonElement>($"/api/rooms/{code}", Ct);
            room.GetProperty("members").GetArrayLength().ShouldBe(8);
            foreach (var r in responses)
            {
                r.Dispose();
            }

            foreach (var c in clients)
            {
                c.Dispose();
            }
        }
    }

    [Fact]
    public async Task Close_ByHost_EndsJoinsButMembersStillSeeIt()
    {
        using var host = await BrowserClient.RegisteredAsync(_factory, Ct);
        var code = Code(await CreateAsync(host));
        using var guest = await JoinAsGuestAsync(code, "Omar");

        using var byGuest = await guest.PostAsync(new Uri($"/api/rooms/{code}/close", UriKind.Relative), null, Ct);
        using var byHost = await host.PostAsync(new Uri($"/api/rooms/{code}/close", UriKind.Relative), null, Ct);
        using var again = await host.PostAsync(new Uri($"/api/rooms/{code}/close", UriKind.Relative), null, Ct);
        using var late = await BrowserClient.StartAsync(_factory, Ct);
        using var join = await late.PostAsJsonAsync($"/api/rooms/{code}/join", new { displayName = "Late" }, Ct);

        byGuest.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await ProblemCodeAsync(byGuest)).ShouldBe("room.not_host");
        byHost.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        again.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        join.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await ProblemCodeAsync(join)).ShouldBe("room.closed");
        (await guest.GetFromJsonAsync<JsonElement>($"/api/rooms/{code}", Ct)).GetProperty("status").GetString().ShouldBe("closed");
    }

    [Fact]
    public async Task Leave_Guest_EndsTheGuestSession()
    {
        using var host = await BrowserClient.RegisteredAsync(_factory, Ct);
        var code = Code(await CreateAsync(host));
        using var guest = await JoinAsGuestAsync(code, "Omar");

        using var response = await guest.PostAsync(new Uri($"/api/rooms/{code}/leave", UriKind.Relative), null, Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await guest.GetAsync("/api/auth/me", Ct)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await host.GetFromJsonAsync<JsonElement>($"/api/rooms/{code}", Ct)).GetProperty("members").GetArrayLength().ShouldBe(1);
    }

    [Fact]
    public async Task Leave_Host_PassesHostingToTheLongestPresentMember_AndTheLastOneOutCloses()
    {
        using var host = await BrowserClient.RegisteredAsync(_factory, Ct, "Ada");
        var code = Code(await CreateAsync(host));
        using var first = await JoinAsGuestAsync(code, "Omar");
        using var second = await JoinAsGuestAsync(code, "Lina");

        (await host.PostAsync(new Uri($"/api/rooms/{code}/leave", UriKind.Relative), null, Ct)).StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var room = await first.GetFromJsonAsync<JsonElement>($"/api/rooms/{code}", Ct);
        room.GetProperty("me").GetProperty("isHost").GetBoolean().ShouldBeTrue();
        (await _factory.CreateClient().GetFromJsonAsync<JsonElement>($"/api/rooms/{code}/preview", Ct)).GetProperty("hostDisplayName").GetString().ShouldBe("Omar");
        (await second.PostAsync(new Uri($"/api/rooms/{code}/close", UriKind.Relative), null, Ct)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await first.PostAsync(new Uri($"/api/rooms/{code}/close", UriKind.Relative), null, Ct)).StatusCode.ShouldBe(HttpStatusCode.NoContent);

        using var host2 = await BrowserClient.RegisteredAsync(_factory, Ct);
        var code2 = Code(await CreateAsync(host2));
        using var only = await JoinAsGuestAsync(code2, "Omar");
        (await host2.PostAsync(new Uri($"/api/rooms/{code2}/leave", UriKind.Relative), null, Ct)).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await only.PostAsync(new Uri($"/api/rooms/{code2}/leave", UriKind.Relative), null, Ct)).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await _factory.CreateClient().GetFromJsonAsync<JsonElement>($"/api/rooms/{code2}/preview", Ct)).GetProperty("status").GetString().ShouldBe("closed");
    }

    [Fact]
    public async Task Leave_ThenJoinAgain_UserKeepsTheSameMembership()
    {
        using var host = await BrowserClient.RegisteredAsync(_factory, Ct, "Ada");
        var code = Code(await CreateAsync(host));
        using var user = await BrowserClient.RegisteredAsync(_factory, Ct, "Sam");
        var joined = await JoinAsync(user, code);

        (await host.PostAsync(new Uri($"/api/rooms/{code}/leave", UriKind.Relative), null, Ct)).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        var back = await JoinAsync(host, code);

        back.GetProperty("me").GetProperty("isHost").GetBoolean().ShouldBeFalse();
        back.GetProperty("members")[0].GetProperty("id").GetString().ShouldBe(joined.GetProperty("me").GetProperty("id").GetString());
        back.GetProperty("members").GetArrayLength().ShouldBe(2);
    }

    [Fact]
    public async Task Leave_NotAMember_IsForbidden()
    {
        using var host = await BrowserClient.RegisteredAsync(_factory, Ct);
        var code = Code(await CreateAsync(host));
        using var stranger = await BrowserClient.RegisteredAsync(_factory, Ct);

        using var response = await stranger.PostAsync(new Uri($"/api/rooms/{code}/leave", UriKind.Relative), null, Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task GuestUpgrade_Register_MovesTheMembershipToTheNewUser()
    {
        using var host = await BrowserClient.RegisteredAsync(_factory, Ct);
        var code = Code(await CreateAsync(host));
        using var guest = await BrowserClient.StartAsync(_factory, Ct);
        var asGuest = await JoinAsync(guest, code, displayName: "Omar");

        using var register = await guest.PostAsJsonAsync("/api/auth/register", new { email = BrowserClient.UniqueEmail(), password = "correct horse", displayName = "Omar K" }, Ct);
        register.StatusCode.ShouldBe(HttpStatusCode.Created, await register.Content.ReadAsStringAsync(Ct));

        var room = await guest.GetFromJsonAsync<JsonElement>($"/api/rooms/{code}", Ct);
        room.GetProperty("me").GetProperty("id").GetString().ShouldBe(asGuest.GetProperty("me").GetProperty("id").GetString());
        room.GetProperty("me").GetProperty("kind").GetString().ShouldBe("user");
        room.GetProperty("me").GetProperty("displayName").GetString().ShouldBe("Omar K");
        room.GetProperty("members").GetArrayLength().ShouldBe(2);
    }

    [Fact]
    public async Task GuestUpgrade_SignIn_MovesTheMembership_UnlessTheUserIsAlreadyThere()
    {
        using var host = await BrowserClient.RegisteredAsync(_factory, Ct);
        var code = Code(await CreateAsync(host));
        var email = BrowserClient.UniqueEmail();
        using (var account = await BrowserClient.StartAsync(_factory, Ct))
        {
            (await account.PostAsJsonAsync("/api/auth/register", new { email, password = "correct horse", displayName = "Lina" }, Ct)).EnsureSuccessStatusCode();
        }

        using var guest = await JoinAsGuestAsync(code, "Lina on her phone");
        (await guest.PostAsJsonAsync("/api/auth/sign-in", new { email, password = "correct horse" }, Ct)).EnsureSuccessStatusCode();
        var room = await guest.GetFromJsonAsync<JsonElement>($"/api/rooms/{code}", Ct);
        room.GetProperty("me").GetProperty("kind").GetString().ShouldBe("user");
        room.GetProperty("me").GetProperty("displayName").GetString().ShouldBe("Lina");

        // Already present as that user: the guest's place ends, the user's stays.
        using var secondGuest = await JoinAsGuestAsync(code, "Lina again");
        (await secondGuest.PostAsJsonAsync("/api/auth/sign-in", new { email, password = "correct horse" }, Ct)).EnsureSuccessStatusCode();
        var after = await secondGuest.GetFromJsonAsync<JsonElement>($"/api/rooms/{code}", Ct);
        after.GetProperty("me").GetProperty("id").GetString().ShouldBe(room.GetProperty("me").GetProperty("id").GetString());
        after.GetProperty("members").GetArrayLength().ShouldBe(2);
    }

    private async Task<HttpClient> JoinAsGuestAsync(string code, string name)
    {
        var client = await BrowserClient.StartAsync(_factory, Ct);
        await JoinAsync(client, code, name);
        return client;
    }

    private static async Task<JsonElement> JoinAsync(HttpClient client, string code, string? displayName = null)
    {
        using var response = await client.PostAsJsonAsync($"/api/rooms/{code}/join", new { displayName }, Ct);
        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync(Ct));
        return await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
    }

    internal static async Task<JsonElement> CreateAsync(HttpClient host, string name = "Friday Film Club", string mode = "justSwipe")
    {
        using var response = await host.PostAsJsonAsync("/api/rooms", new { name, mode, startsAt = (string?)null }, Ct);
        response.StatusCode.ShouldBe(HttpStatusCode.Created, await response.Content.ReadAsStringAsync(Ct));
        return await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
    }

    internal static string Code(JsonElement room) => room.GetProperty("code").GetString()!;

    private static async Task<string?> ProblemCodeAsync(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("code").GetString();
}
