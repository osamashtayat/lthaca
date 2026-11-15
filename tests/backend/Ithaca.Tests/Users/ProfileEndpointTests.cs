using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Ithaca.Application.Realtime;
using Ithaca.Domain.Rooms;
using Ithaca.Infrastructure.Persistence;
using Ithaca.Tests.Harness;
using Ithaca.Tests.Rooms;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Ithaca.Tests.Users;

/// <summary>
/// BE-16 (BE-16a's contract): the profile (api-contract.md §14). Users only; display name; password change and account
/// deletion re-authenticate with the sign-in lockout, issue a fresh <c>XSRF-TOKEN</c>, and end the user's other sessions.
/// </summary>
[Collection(DatabaseCollectionDefinition.Name)]
public sealed class ProfileEndpointTests(PostgresFixture postgres) : IAsyncLifetime
{
    private const string Password = "correct horse";
    private const string NewPassword = "battery staple";

    private readonly RecordingPublisher _events = new();
    private IthacaApiFactory _base = null!;
    private WebApplicationFactory<Program> _factory = null!;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync()
    {
        _base = await IthacaApiFactory.StartAsync(postgres.ConnectionString);
        _factory = _base.WithWebHostBuilder(b => b.ConfigureTestServices(s => s.AddSingleton<IRoomEventPublisher>(_events)));
    }

    public async ValueTask DisposeAsync()
    {
        await _factory.DisposeAsync();
        await _base.DisposeAsync();
    }

    /// <summary>Method, path and a valid JSON body.</summary>
    public static TheoryData<string, string, string?> Actions => new()
    {
        { "GET", "/api/profile", null },
        { "PUT", "/api/profile", """{"displayName":"Lina"}""" },
        { "PUT", "/api/profile/password", """{"currentPassword":"correct horse","newPassword":"battery staple"}""" },
        { "DELETE", "/api/profile", """{"password":"correct horse"}""" },
    };

    [Fact]
    public async Task Get_ReturnsTheUsersProfile()
    {
        var email = BrowserClient.UniqueEmail();
        using var user = await RegisterAsync(email, "Ada");
        var me = await user.GetFromJsonAsync<JsonElement>("/api/auth/me", Ct);

        var profile = await user.GetFromJsonAsync<JsonElement>("/api/profile", Ct);

        profile.GetProperty("id").GetString().ShouldBe(me.GetProperty("id").GetString());
        profile.GetProperty("displayName").GetString().ShouldBe("Ada");
        profile.GetProperty("email").GetString().ShouldBe(email);
        profile.GetProperty("createdAt").GetDateTimeOffset().ShouldBe(DateTimeOffset.UtcNow, TimeSpan.FromMinutes(1));
    }

    [Fact]
    public async Task Update_TrimsTheName_AndMeAndRoomsShowItAtOnce()
    {
        using var user = await BrowserClient.RegisteredAsync(_factory, Ct, "Ada");
        var room = await RoomsEndpointTests.CreateAsync(user);

        using var response = await user.PutAsJsonAsync("/api/profile", new { displayName = "  Lina  " }, Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync(Ct));
        (await response.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("displayName").GetString().ShouldBe("Lina");
        (await user.GetFromJsonAsync<JsonElement>("/api/auth/me", Ct)).GetProperty("displayName").GetString().ShouldBe("Lina");
        var inRoom = await user.GetFromJsonAsync<JsonElement>($"/api/rooms/{RoomsEndpointTests.Code(room)}", Ct);
        inRoom.GetProperty("me").GetProperty("displayName").GetString().ShouldBe("Lina", "a user member's name is the account's (owner, BE-16)");
    }

    [Fact]
    public async Task Update_BlankAfterTrimming_IsValidationOnDisplayName()
    {
        using var user = await BrowserClient.RegisteredAsync(_factory, Ct);

        using var response = await user.PutAsJsonAsync("/api/profile", new { displayName = "   " }, Ct);

        await ShouldBeValidationOnAsync(response, "displayName");
    }

    [Theory]
    [MemberData(nameof(Actions))]
    public async Task Action_GuestOrAnonymous_IsUnauthenticated(string method, string path, string? body)
    {
        using var host = await BrowserClient.RegisteredAsync(_factory, Ct);
        var code = RoomsEndpointTests.Code(await RoomsEndpointTests.CreateAsync(host));
        using var guest = await BrowserClient.StartAsync(_factory, Ct);
        (await guest.PostAsJsonAsync($"/api/rooms/{code}/join", new { displayName = "Omar" }, Ct)).EnsureSuccessStatusCode();
        using var anonymous = await BrowserClient.StartAsync(_factory, Ct);

        foreach (var client in new[] { guest, anonymous })
        {
            using var response = await client.SendAsync(Request(method, path, body), Ct);

            response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
            (await CodeAsync(response)).ShouldBe("auth.unauthenticated");
        }
    }

    [Theory]
    [InlineData("PUT", "/api/profile", """{"displayName":""}""", "displayName")]
    [InlineData("PUT", "/api/profile", """{"displayName":"12345678901234567890123456789012345678901"}""", "displayName")]
    [InlineData("PUT", "/api/profile/password", """{"currentPassword":"correct horse","newPassword":"short"}""", "newPassword")]
    [InlineData("PUT", "/api/profile/password", """{"currentPassword":"wrong one","newPassword":"short"}""", "newPassword")]
    [InlineData("PUT", "/api/profile/password", """{"currentPassword":"","newPassword":"battery staple"}""", "currentPassword")]
    [InlineData("DELETE", "/api/profile", """{"password":""}""", "password")]
    public async Task Action_InvalidBody_IsValidationErrorOnTheField_BeforeThePasswordCheck(string method, string path, string body, string field)
    {
        using var user = await BrowserClient.RegisteredAsync(_factory, Ct);

        using var response = await user.SendAsync(Request(method, path, body), Ct);

        await ShouldBeValidationOnAsync(response, field);
    }

    [Fact]
    public async Task ChangePassword_WrongCurrent_IsWrongPassword_AndNothingChanges()
    {
        var email = BrowserClient.UniqueEmail();
        using var user = await RegisterAsync(email);

        using var response = await user.PutAsJsonAsync("/api/profile/password", new { currentPassword = "not it at all", newPassword = NewPassword }, Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await CodeAsync(response)).ShouldBe("profile.wrong_password");
        (await SignInAsync(email, Password)).ShouldBe(HttpStatusCode.OK);
        (await SignInAsync(email, NewPassword)).ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ChangePassword_KeepsThisSession_EndsTheOthers_AndTheNewPasswordSignsIn()
    {
        var email = BrowserClient.UniqueEmail();
        using var user = await RegisterAsync(email);
        using var otherDevice = await BrowserClient.StartAsync(_factory, Ct);
        (await otherDevice.PostAsJsonAsync("/api/auth/sign-in", new { email, password = Password }, Ct)).EnsureSuccessStatusCode();

        using var response = await user.PutAsJsonAsync("/api/profile/password", new { currentPassword = Password, newPassword = NewPassword }, Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent, await response.Content.ReadAsStringAsync(Ct));
        BrowserClient.SetCookie(response, "XSRF-TOKEN").ShouldNotBeNull();
        BrowserClient.SetCookie(response, "ithaca_session").ShouldNotBeNull();
        (await user.GetAsync("/api/profile", Ct)).StatusCode.ShouldBe(HttpStatusCode.OK);
        using (var afterwards = await user.PutAsJsonAsync("/api/profile", new { displayName = "Still me" }, Ct))
        {
            afterwards.StatusCode.ShouldBe(HttpStatusCode.OK, "the new XSRF-TOKEN works for this session");
        }

        (await otherDevice.GetAsync("/api/profile", Ct)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await SignInAsync(email, NewPassword)).ShouldBe(HttpStatusCode.OK);
        (await SignInAsync(email, Password)).ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ChangePassword_SameAsTheOld_IsAllowed()
    {
        using var user = await BrowserClient.RegisteredAsync(_factory, Ct);

        using var response = await user.PutAsJsonAsync("/api/profile/password", new { currentPassword = Password, newPassword = Password }, Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task WrongPasswords_CountTowardTheSignInLockout_AndALockedAccountAnswersTheSame()
    {
        var email = BrowserClient.UniqueEmail();
        using var user = await RegisterAsync(email);
        for (var i = 0; i < 5; i++)
        {
            using var wrong = await user.SendAsync(Request("DELETE", "/api/profile", """{"password":"not it at all"}"""), Ct);
            (await CodeAsync(wrong)).ShouldBe("profile.wrong_password");
        }

        using var right = await user.PutAsJsonAsync("/api/profile/password", new { currentPassword = Password, newPassword = NewPassword }, Ct);

        right.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await CodeAsync(right)).ShouldBe("profile.wrong_password", "locked out: never revealed, nothing changed");
        (await SignInAsync(email, Password)).ShouldBe(HttpStatusCode.Unauthorized, "sign-in is locked out too");
        (await user.GetAsync("/api/profile", Ct)).StatusCode.ShouldBe(HttpStatusCode.OK, "the account is still there");
    }

    [Fact]
    public async Task Delete_WrongPassword_IsWrongPassword_AndNothingIsDeleted()
    {
        using var user = await BrowserClient.RegisteredAsync(_factory, Ct);

        using var response = await user.SendAsync(Request("DELETE", "/api/profile", """{"password":"not it at all"}"""), Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await CodeAsync(response)).ShouldBe("profile.wrong_password");
        (await user.GetAsync("/api/profile", Ct)).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Delete_RemovesTheAccountAndWhatItOwns_SignsOut_AndTheEmailCanRegisterAgain()
    {
        var email = BrowserClient.UniqueEmail();
        using var user = await RegisterAsync(email);
        var userId = Guid.Parse((await user.GetFromJsonAsync<JsonElement>("/api/auth/me", Ct)).GetProperty("id").GetString()!);
        (await user.PutAsJsonAsync("/api/library/550", new { liked = true, onWatchlist = false, seen = false, rating = (decimal?)null }, Ct)).EnsureSuccessStatusCode();
        using var otherDevice = await BrowserClient.StartAsync(_factory, Ct);
        (await otherDevice.PostAsJsonAsync("/api/auth/sign-in", new { email, password = Password }, Ct)).EnsureSuccessStatusCode();

        using var response = await user.SendAsync(Request("DELETE", "/api/profile", """{"password":"correct horse"}"""), Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent, await response.Content.ReadAsStringAsync(Ct));
        BrowserClient.SetCookie(response, "XSRF-TOKEN").ShouldNotBeNull();
        BrowserClient.SetCookie(response, "ithaca_session")!.ShouldContain("expires=Thu, 01 Jan 1970", Case.Insensitive);
        (await user.GetAsync("/api/auth/me", Ct)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await otherDevice.GetAsync("/api/profile", Ct)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            (await db.Users.AnyAsync(u => u.Id == userId, Ct)).ShouldBeFalse();
            (await db.UserFilms.AnyAsync(u => u.UserId == userId, Ct)).ShouldBeFalse();
            (await db.Films.AnyAsync(f => f.TmdbId == 550, Ct)).ShouldBeTrue("the catalog is never removed (ADR-0019)");
        }

        using var again = await BrowserClient.StartAsync(_factory, Ct);
        using var registered = await again.PostAsJsonAsync("/api/auth/register", new { email, password = Password, displayName = "Ada" }, Ct);
        registered.StatusCode.ShouldBe(HttpStatusCode.Created);
    }

    [Fact]
    public async Task Delete_ClosesTheOpenRoomsTheUserHosts_AndLeavesTheOthers_ThenAnnouncesIt()
    {
        using var user = await BrowserClient.RegisteredAsync(_factory, Ct, "Ada");
        using var friend = await BrowserClient.RegisteredAsync(_factory, Ct, "Bea");
        var hosted = await RoomsEndpointTests.CreateAsync(user, "Ada's room");
        var hostedCode = RoomsEndpointTests.Code(hosted);
        (await friend.PostAsJsonAsync($"/api/rooms/{hostedCode}/join", new { displayName = (string?)null }, Ct)).EnsureSuccessStatusCode();
        var joined = await RoomsEndpointTests.CreateAsync(friend, "Bea's room");
        var joinedCode = RoomsEndpointTests.Code(joined);
        using var join = await user.PostAsJsonAsync($"/api/rooms/{joinedCode}/join", new { displayName = (string?)null }, Ct);
        var memberId = Guid.Parse((await join.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("me").GetProperty("id").GetString()!);
        var hostedId = Guid.Parse(hosted.GetProperty("id").GetString()!);
        var joinedId = Guid.Parse(joined.GetProperty("id").GetString()!);

        using var response = await user.SendAsync(Request("DELETE", "/api/profile", """{"password":"correct horse"}"""), Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent, await response.Content.ReadAsStringAsync(Ct));
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            (await db.Rooms.SingleAsync(r => r.Id == hostedId, Ct)).Status.ShouldBe(RoomStatus.Closed);
            (await db.Rooms.SingleAsync(r => r.Id == joinedId, Ct)).Status.ShouldBe(RoomStatus.Lobby);
            (await db.RoomMembers.AnyAsync(m => m.Id == memberId, Ct)).ShouldBeFalse();
        }

        _events.Of(hostedId, RoomEvents.RoomClosed).ShouldHaveSingleItem();
        _events.Of(joinedId, RoomEvents.RoomClosed).ShouldBeEmpty();
        _events.Of(joinedId, RoomEvents.MemberLeft).ShouldContain(new RoomMemberEvent(joinedId, memberId));
        var bea = await friend.GetFromJsonAsync<JsonElement>($"/api/rooms/{joinedCode}", Ct);
        bea.GetProperty("members").GetArrayLength().ShouldBe(1);
    }

    [Fact]
    public async Task Delete_WithoutXsrfHeader_IsRefused()
    {
        using var user = await BrowserClient.RegisteredAsync(_factory, Ct);
        using var plain = _factory.CreateClient();
        using var request = Request("DELETE", "/api/profile", """{"password":"correct horse"}""");
        request.Headers.Add("Cookie", BrowserClient.CookieHeader(user));

        using var response = await plain.SendAsync(request, Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await CodeAsync(response)).ShouldBe("auth.antiforgery_failed");
    }

    private async Task<HttpClient> RegisterAsync(string email, string displayName = "Ada")
    {
        var client = await BrowserClient.StartAsync(_factory, Ct);
        using var response = await client.PostAsJsonAsync("/api/auth/register", new { email, password = Password, displayName }, Ct);
        response.EnsureSuccessStatusCode();
        return client;
    }

    private async Task<HttpStatusCode> SignInAsync(string email, string password)
    {
        using var client = await BrowserClient.StartAsync(_factory, Ct);
        using var response = await client.PostAsJsonAsync("/api/auth/sign-in", new { email, password }, Ct);
        return response.StatusCode;
    }

    private static async Task ShouldBeValidationOnAsync(HttpResponseMessage response, string field)
    {
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        using var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct));
        problem.RootElement.GetProperty("code").GetString().ShouldBe("validation");
        problem.RootElement.GetProperty("errors").EnumerateObject().Select(p => p.Name).ShouldContain(field, problem.RootElement.ToString());
    }

    private static HttpRequestMessage Request(string method, string path, string? body)
    {
        var request = new HttpRequestMessage(new HttpMethod(method), new Uri(path, UriKind.Relative));
        if (body is not null)
        {
            request.Content = new StringContent(body, Encoding.UTF8, "application/json");
        }

        return request;
    }

    private static async Task<string?> CodeAsync(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("code").GetString();

    /// <summary>Records every published event instead of sending it.</summary>
    private sealed class RecordingPublisher : IRoomEventPublisher
    {
        private readonly ConcurrentQueue<(Guid RoomId, string Name, object Payload)> _published = new();

        public Task PublishAsync<TPayload>(Guid roomId, string eventName, TPayload payload, CancellationToken ct)
            where TPayload : notnull
        {
            _published.Enqueue((roomId, eventName, payload));
            return Task.CompletedTask;
        }

        public List<object> Of(Guid roomId, string eventName) =>
            _published.Where(e => e.RoomId == roomId && e.Name == eventName).Select(e => e.Payload).ToList();
    }
}
