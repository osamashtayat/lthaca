using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using Ithaca.Infrastructure.Email;
using Ithaca.Infrastructure.Identity;
using Ithaca.Infrastructure.Persistence;
using Ithaca.Tests.Harness;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Ithaca.Tests.Auth;

/// <summary>
/// Issue #64 (ADR-0030): ASP.NET Core Data Protection keys live in PostgreSQL (<c>data_protection_keys</c>) with a stable
/// application name so user sessions, guest sessions and password-reset links survive host restarts and redeploys.
/// </summary>
[Collection(DatabaseCollectionDefinition.Name)]
public sealed partial class DataProtectionPersistenceTests(PostgresFixture postgres)
{
    private const string Password = "correct horse";
    private const string NewPassword = "battery staple";
    private static readonly Uri Me = new("/api/auth/me", UriKind.Relative);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task KeyRing_UsesPostgresRepository_AndStableApplicationName()
    {
        await using var factory = await IthacaApiFactory.StartAsync(postgres.ConnectionString);

        var dataProtection = factory.Services.GetRequiredService<IOptions<DataProtectionOptions>>().Value;
        var keyManagement = factory.Services.GetRequiredService<IOptions<KeyManagementOptions>>().Value;

        dataProtection.ApplicationDiscriminator.ShouldBe(IdentitySetup.DataProtectionApplicationName);
        keyManagement.XmlRepository.ShouldBeOfType<EntityFrameworkCoreXmlRepository<AppDbContext>>();
    }

    [Fact]
    public async Task UserSessionCookie_SurvivesHostRestart()
    {
        var email = BrowserClient.UniqueEmail();
        string userId;
        string cookie;

        await using (var first = await IthacaApiFactory.StartAsync(postgres.ConnectionString))
        {
            using var client = await BrowserClient.StartAsync(first, Ct);
            using var registered = await client.PostAsJsonAsync(
                new Uri("/api/auth/register", UriKind.Relative),
                new { email, password = Password, displayName = "Ada" },
                Ct);
            registered.StatusCode.ShouldBe(HttpStatusCode.Created);
            userId = (await registered.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("id").GetString()!;
            cookie = BrowserClient.CookieHeader(client);
            cookie.ShouldContain("ithaca_session=");

            await using var scope = first.Services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            (await db.DataProtectionKeys.AsNoTracking().CountAsync(Ct)).ShouldBeGreaterThan(0);
        }

        // Fresh host against the same PostgreSQL database: simulates container restart / redeploy.
        await using var second = await IthacaApiFactory.StartAsync(postgres.ConnectionString);
        using var restarted = second.CreateClient();
        restarted.DefaultRequestHeaders.Add("Cookie", cookie);

        using var response = await restarted.GetAsync(Me, Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync(Ct));
        var me = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
        me.GetProperty("id").GetString().ShouldBe(userId);
        me.GetProperty("email").GetString().ShouldBe(email);
    }

    [Fact]
    public async Task GuestSessionCookie_SurvivesHostRestart()
    {
        string roomCode;
        string cookie;

        await using (var first = await IthacaApiFactory.StartAsync(postgres.ConnectionString))
        {
            using var host = await BrowserClient.RegisteredAsync(first, Ct, "Host");
            using var created = await host.PostAsJsonAsync(
                new Uri("/api/rooms", UriKind.Relative),
                new { name = "Friday Night", mode = "justSwipe", startsAt = (string?)null },
                Ct);
            created.StatusCode.ShouldBe(HttpStatusCode.Created);
            roomCode = (await created.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("code").GetString()!;

            using var guest = await BrowserClient.StartAsync(first, Ct);
            using var joined = await guest.PostAsJsonAsync(
                new Uri($"/api/rooms/{roomCode}/join", UriKind.Relative),
                new { displayName = "Omar" },
                Ct);
            joined.StatusCode.ShouldBe(HttpStatusCode.OK);
            cookie = BrowserClient.CookieHeader(guest);
            cookie.ShouldContain("ithaca_session=");
        }

        await using var second = await IthacaApiFactory.StartAsync(postgres.ConnectionString);
        using var restarted = second.CreateClient();
        restarted.DefaultRequestHeaders.Add("Cookie", cookie);

        using var response = await restarted.GetAsync(Me, Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync(Ct));
        var me = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
        me.GetProperty("kind").GetString().ShouldBe("guest");
        me.GetProperty("displayName").GetString().ShouldBe("Omar");
        me.GetProperty("roomCode").GetString().ShouldBe(roomCode);
    }

    [Fact]
    public async Task PasswordResetLink_SurvivesHostRestart()
    {
        var email = BrowserClient.UniqueEmail();
        string token;

        await using (var first = await IthacaApiFactory.StartAsync(postgres.ConnectionString))
        {
            using var user = await BrowserClient.StartAsync(first, Ct);
            (await user.PostAsJsonAsync(
                new Uri("/api/auth/register", UriKind.Relative),
                new { email, password = Password, displayName = "Lina" },
                Ct)).EnsureSuccessStatusCode();

            using var visitor = await BrowserClient.StartAsync(first, Ct);
            (await visitor.PostAsJsonAsync(new Uri("/api/auth/forgot-password", UriKind.Relative), new { email }, Ct))
                .StatusCode.ShouldBe(HttpStatusCode.NoContent);

            var outbox = first.Services.GetRequiredService<FakeEmailSender>();
            var message = outbox.Sent.Single(m => m.To == email);
            token = Uri.UnescapeDataString(TokenInLink().Match(message.Text).Groups[1].Value);
        }

        await using var second = await IthacaApiFactory.StartAsync(postgres.ConnectionString);
        using var restarted = await BrowserClient.StartAsync(second, Ct);
        using var reset = await restarted.PostAsJsonAsync(
            new Uri("/api/auth/reset-password", UriKind.Relative),
            new { token, newPassword = NewPassword },
            Ct);

        reset.StatusCode.ShouldBe(HttpStatusCode.OK, await reset.Content.ReadAsStringAsync(Ct));
        using var signInClient = await BrowserClient.StartAsync(second, Ct);
        using var signedIn = await signInClient.PostAsJsonAsync(
            new Uri("/api/auth/sign-in", UriKind.Relative),
            new { email, password = NewPassword },
            Ct);
        signedIn.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [GeneratedRegex(@"/reset-password\?token=(\S+)")]
    private static partial Regex TokenInLink();
}
