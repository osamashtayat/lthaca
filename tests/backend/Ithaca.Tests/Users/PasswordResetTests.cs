using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using Ithaca.Infrastructure.Email;
using Ithaca.Infrastructure.Persistence;
using Ithaca.Tests.Harness;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Ithaca.Tests.Users;

/// <summary>
/// BE-18: password reset by email (api-contract.md §4) end to end on the real API with the Fake email sender: the link,
/// one hour, one use, any password change invalidating links, lockout lifted, other sessions ended, guest upgrade.
/// </summary>
[Collection(DatabaseCollectionDefinition.Name)]
public sealed partial class PasswordResetTests(PostgresFixture postgres) : IAsyncLifetime
{
    private const string Password = "correct horse";
    private const string NewPassword = "battery staple";

    private readonly ShiftableClock _clock = new();
    private IthacaApiFactory _base = null!;
    private WebApplicationFactory<Program> _factory = null!;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync()
    {
        _base = await IthacaApiFactory.StartAsync(postgres.ConnectionString);
        _factory = _base.WithWebHostBuilder(b => b.ConfigureTestServices(s => s.AddSingleton<TimeProvider>(_clock)));
    }

    public async ValueTask DisposeAsync()
    {
        await _factory.DisposeAsync();
        await _base.DisposeAsync();
    }

    [Fact]
    public async Task Forgot_ForAnAccount_EmailsALinkToTheSite()
    {
        var (email, _) = await RegisterAsync("Lina");

        using var response = await ForgotAsync(email);

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        var message = Outbox.Sent.Single(m => m.To == email);
        message.Subject.ShouldBe("Reset your Ithaca password");
        message.Text.ShouldContain("Hi Lina,");
        message.Text.ShouldContain("http://localhost:5273/reset-password?token=");
        message.Html.ShouldContain("http://localhost:5273/reset-password?token=");
    }

    [Fact]
    public async Task Forgot_ForAnUnknownEmail_IsTheSameAnswer_AndSendsNothing()
    {
        var email = BrowserClient.UniqueEmail();

        using var response = await ForgotAsync(email);

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        Outbox.Sent.ShouldNotContain(m => m.To == email);
    }

    [Fact]
    public async Task Reset_WithTheLink_SetsThePassword_AndSignsIn()
    {
        var (email, _) = await RegisterAsync("Lina");
        var token = await RequestTokenAsync(email);
        using var visitor = await BrowserClient.StartAsync(_factory, Ct);

        using var response = await ResetAsync(visitor, token);

        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync(Ct));
        var user = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
        user.GetProperty("kind").GetString().ShouldBe("user");
        user.GetProperty("email").GetString().ShouldBe(email);
        BrowserClient.SetCookie(response, "XSRF-TOKEN").ShouldNotBeNull();
        (await visitor.GetAsync("/api/auth/me", Ct)).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await SignInAsync(email, NewPassword)).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await SignInAsync(email, Password)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Links_WorkOnce_AndUsingOneEndsTheOthers()
    {
        var (email, _) = await RegisterAsync("Lina");
        var first = await RequestTokenAsync(email);
        var second = await RequestTokenAsync(email);
        using var visitor = await BrowserClient.StartAsync(_factory, Ct);

        (await ResetAsync(visitor, second)).StatusCode.ShouldBe(HttpStatusCode.OK); // earlier links stay valid until one is used

        await ShouldBeInvalidAsync(await ResetAsync(visitor, second));
        await ShouldBeInvalidAsync(await ResetAsync(visitor, first));
    }

    [Fact]
    public async Task Link_ExpiresAfterOneHour()
    {
        var (email, _) = await RegisterAsync("Lina");
        var token = await RequestTokenAsync(email);
        using var visitor = await BrowserClient.StartAsync(_factory, Ct);

        _clock.Offset = TimeSpan.FromHours(1) + TimeSpan.FromSeconds(1);
        try
        {
            await ShouldBeInvalidAsync(await ResetAsync(visitor, token));
        }
        finally
        {
            _clock.Offset = TimeSpan.Zero;
        }

        (await SignInAsync(email, Password)).StatusCode.ShouldBe(HttpStatusCode.OK); // nothing changed
        (await ResetAsync(visitor, token)).StatusCode.ShouldBe(HttpStatusCode.OK); // and within the hour the same link works
    }

    [Fact]
    public async Task Link_IsInvalid_AfterAProfilePasswordChange()
    {
        var (email, user) = await RegisterAsync("Lina");
        var token = await RequestTokenAsync(email);
        (await user.PutAsJsonAsync("/api/profile/password", new { currentPassword = Password, newPassword = NewPassword }, Ct)).EnsureSuccessStatusCode();
        using var visitor = await BrowserClient.StartAsync(_factory, Ct);

        await ShouldBeInvalidAsync(await ResetAsync(visitor, token, "another password"));
    }

    [Theory]
    [InlineData("not-a-token")]
    [InlineData("AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA")]
    public async Task Reset_WithAForgedToken_IsInvalid(string token)
    {
        using var visitor = await BrowserClient.StartAsync(_factory, Ct);

        await ShouldBeInvalidAsync(await ResetAsync(visitor, token));
    }

    [Fact]
    public async Task Reset_LiftsALockout()
    {
        var (email, _) = await RegisterAsync("Lina");
        for (var i = 0; i < 5; i++)
        {
            (await SignInAsync(email, "wrong password")).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        }

        (await SignInAsync(email, Password)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized); // locked out
        var token = await RequestTokenAsync(email);
        using var visitor = await BrowserClient.StartAsync(_factory, Ct);

        (await ResetAsync(visitor, token)).StatusCode.ShouldBe(HttpStatusCode.OK);

        (await SignInAsync(email, NewPassword)).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Reset_EndsTheUsersOtherSessions()
    {
        var (email, otherSession) = await RegisterAsync("Lina");
        (await otherSession.GetAsync("/api/auth/me", Ct)).StatusCode.ShouldBe(HttpStatusCode.OK);
        var token = await RequestTokenAsync(email);
        using var visitor = await BrowserClient.StartAsync(_factory, Ct);

        (await ResetAsync(visitor, token)).StatusCode.ShouldBe(HttpStatusCode.OK);

        (await otherSession.GetAsync("/api/auth/me", Ct)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Reset_ByAGuest_MovesTheirRoomMembershipToTheUser()
    {
        var (email, _) = await RegisterAsync("Lina");
        using var host = await BrowserClient.RegisteredAsync(_factory, Ct, "Host");
        using var created = await host.PostAsJsonAsync("/api/rooms", new { name = "Room", mode = "justSwipe", startsAt = (string?)null }, Ct);
        var code = (await created.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("code").GetString();
        using var guest = await BrowserClient.StartAsync(_factory, Ct);
        (await guest.PostAsJsonAsync($"/api/rooms/{code}/join", new { displayName = "Lina as guest" }, Ct)).EnsureSuccessStatusCode();
        var token = await RequestTokenAsync(email);

        using var response = await ResetAsync(guest, token);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var userId = (await response.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("id").GetGuid();
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        (await db.RoomMembers.CountAsync(m => m.UserId == userId && m.LeftAt == null, Ct)).ShouldBe(1);
    }

    private FakeEmailSender Outbox => _factory.Services.GetRequiredService<FakeEmailSender>();

    private async Task<(string Email, HttpClient Client)> RegisterAsync(string displayName)
    {
        var email = BrowserClient.UniqueEmail();
        var client = await BrowserClient.StartAsync(_factory, Ct);
        (await client.PostAsJsonAsync("/api/auth/register", new { email, password = Password, displayName }, Ct)).EnsureSuccessStatusCode();
        return (email, client);
    }

    private async Task<HttpResponseMessage> ForgotAsync(string email)
    {
        using var visitor = await BrowserClient.StartAsync(_factory, Ct);
        return await visitor.PostAsJsonAsync("/api/auth/forgot-password", new { email }, Ct);
    }

    /// <summary>Asks for a link and returns the token from the newest email, URL-decoded as the SPA would.</summary>
    private async Task<string> RequestTokenAsync(string email)
    {
        var before = Outbox.Sent.Count(m => m.To == email);
        (await ForgotAsync(email)).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        var message = Outbox.Sent.Where(m => m.To == email).ToList();
        message.Count.ShouldBe(before + 1);
        return Uri.UnescapeDataString(TokenInLink().Match(message[^1].Text).Groups[1].Value);
    }

    private static Task<HttpResponseMessage> ResetAsync(HttpClient client, string token, string newPassword = NewPassword) =>
        client.PostAsJsonAsync("/api/auth/reset-password", new { token, newPassword }, Ct);

    private async Task<HttpResponseMessage> SignInAsync(string email, string password)
    {
        using var client = await BrowserClient.StartAsync(_factory, Ct);
        return await client.PostAsJsonAsync("/api/auth/sign-in", new { email, password }, Ct);
    }

    private static async Task ShouldBeInvalidAsync(HttpResponseMessage response)
    {
        using (response)
        {
            response.StatusCode.ShouldBe(HttpStatusCode.BadRequest, await response.Content.ReadAsStringAsync(Ct));
            (await response.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("code").GetString().ShouldBe("auth.invalid_reset_token");
        }
    }

    [GeneratedRegex(@"/reset-password\?token=(\S+)")]
    private static partial Regex TokenInLink();
}
