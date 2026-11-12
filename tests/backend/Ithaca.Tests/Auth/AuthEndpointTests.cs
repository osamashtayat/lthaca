using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Ithaca.Application.Auth;
using Ithaca.Domain.Users;
using Ithaca.Infrastructure.Persistence;
using Ithaca.Tests.Harness;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace Ithaca.Tests.Auth;

/// <summary>BE-02: users, guests, the session cookie and antiforgery (api-contract.md §4, ADR-0012, ADR-0023).</summary>
[Collection(DatabaseCollectionDefinition.Name)]
public sealed class AuthEndpointTests(PostgresFixture postgres) : IAsyncLifetime
{
    private const string Password = "correct horse";
    private static readonly Uri Me = new("/api/auth/me", UriKind.Relative);
    private static readonly Uri Register = new("/api/auth/register", UriKind.Relative);
    private static readonly Uri SignIn = new("/api/auth/sign-in", UriKind.Relative);
    private static readonly Uri SignOut = new("/api/auth/sign-out", UriKind.Relative);

    private IthacaApiFactory _factory = null!;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync() => _factory = await IthacaApiFactory.StartAsync(postgres.ConnectionString);

    public ValueTask DisposeAsync() => _factory.DisposeAsync();

    [Fact]
    public async Task Register_SignsInWithASessionCookie_AndMeReturnsTheUser()
    {
        var client = await BrowserClient.StartAsync(_factory, Ct);
        var email = BrowserClient.UniqueEmail();

        using var registered = await client.PostAsJsonAsync(Register, new { email, password = Password, displayName = "  Ada  " }, Ct);

        registered.StatusCode.ShouldBe(HttpStatusCode.Created);
        registered.Headers.Location!.OriginalString.ShouldBe("/api/auth/me");
        var created = await registered.Content.ReadFromJsonAsync<JsonElement>(Ct);
        created.GetProperty("kind").GetString().ShouldBe("user");
        created.GetProperty("displayName").GetString().ShouldBe("Ada");
        created.GetProperty("roomCode").ValueKind.ShouldBe(JsonValueKind.Null);

        var session = BrowserClient.SetCookie(registered, "ithaca_session")!.ToLowerInvariant();
        session.ShouldContain("httponly");
        session.ShouldContain("samesite=lax");
        session.ShouldContain("path=/");
        session.ShouldContain("expires=");
        session.ShouldNotContain("secure"); // Development over plain HTTP (ADR-0009); Production: SessionCookie_IsSecureInProduction
        BrowserClient.SetCookie(registered, "XSRF-TOKEN")!.ToLowerInvariant().ShouldNotContain("httponly");

        var me = await client.GetFromJsonAsync<JsonElement>(Me, Ct);
        me.GetProperty("id").GetString().ShouldBe(created.GetProperty("id").GetString());
        me.GetProperty("email").GetString().ShouldBe(email);
    }

    [Fact]
    public async Task Register_EmailAlreadyTaken_IsConflict_WhateverTheCase()
    {
        var email = BrowserClient.UniqueEmail();
        var first = await BrowserClient.StartAsync(_factory, Ct);
        (await first.PostAsJsonAsync(Register, new { email, password = Password, displayName = "Ada" }, Ct)).EnsureSuccessStatusCode();

        var second = await BrowserClient.StartAsync(_factory, Ct);
        using var response = await second.PostAsJsonAsync(Register, new { email = email.ToUpperInvariant(), password = Password, displayName = "Bea" }, Ct);

        await ShouldBeProblemAsync(response, HttpStatusCode.Conflict, "auth.email_taken");
    }

    [Fact]
    public async Task Register_ShortPassword_IsValidationOnPassword()
    {
        var client = await BrowserClient.StartAsync(_factory, Ct);

        using var response = await client.PostAsJsonAsync(Register, new { email = BrowserClient.UniqueEmail(), password = "short", displayName = "Ada" }, Ct);

        var problem = await ShouldBeProblemAsync(response, HttpStatusCode.BadRequest, "validation");
        problem.GetProperty("errors").TryGetProperty("password", out _).ShouldBeTrue(problem.ToString());
    }

    [Fact]
    public async Task SignIn_CorrectPassword_ReturnsTheUser()
    {
        var email = await RegisterAsync();
        var client = await BrowserClient.StartAsync(_factory, Ct);

        using var response = await client.PostAsJsonAsync(SignIn, new { email = $"  {email.ToUpperInvariant()} ", password = Password }, Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await client.GetFromJsonAsync<JsonElement>(Me, Ct)).GetProperty("email").GetString().ShouldBe(email);
    }

    [Fact]
    public async Task SignIn_WrongPassword_UnknownEmail_AndLockedAccount_AnswerIdentically()
    {
        var email = await RegisterAsync();
        var client = await BrowserClient.StartAsync(_factory, Ct);

        var wrong = await SignInProblemAsync(client, email, "wrong password");
        var unknown = await SignInProblemAsync(client, BrowserClient.UniqueEmail(), Password);
        for (var attempt = 2; attempt <= 5; attempt++)
        {
            await SignInProblemAsync(client, email, "wrong password"); // the 5th failure locks the account for 15 minutes
        }

        var lockedWithTheRightPassword = await SignInProblemAsync(client, email, Password);

        wrong.ShouldBe(unknown);
        lockedWithTheRightPassword.ShouldBe(wrong);
        wrong.ShouldContain("auth.invalid_credentials");
    }

    [Fact]
    public async Task SignOut_ClearsTheSession()
    {
        var client = await BrowserClient.RegisteredAsync(_factory, Ct);

        using var signedOut = await client.PostAsync(SignOut, null, Ct);

        signedOut.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        BrowserClient.SetCookie(signedOut, "ithaca_session")!.ShouldContain("expires=Thu, 01 Jan 1970");
        using var me = await client.GetAsync(Me, Ct);
        await ShouldBeProblemAsync(me, HttpStatusCode.Unauthorized, "auth.unauthenticated");
    }

    [Fact]
    public async Task Me_Anonymous_Is401Json_AndStillIssuesAnXsrfToken()
    {
        using var client = _factory.CreateClient();

        using var response = await client.GetAsync(Me, Ct);

        await ShouldBeProblemAsync(response, HttpStatusCode.Unauthorized, "auth.unauthenticated");
        BrowserClient.SetCookie(response, "XSRF-TOKEN").ShouldNotBeNull();
    }

    [Fact]
    public async Task UserOnlyEndpoint_Anonymous_Is401Json_NeverARedirect()
    {
        using var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        using var response = await client.GetAsync(new Uri("/api/library/summary", UriKind.Relative), Ct);

        await ShouldBeProblemAsync(response, HttpStatusCode.Unauthorized, "auth.unauthenticated");
    }

    [Fact]
    public async Task UnsafeRequest_WithoutTheHeader_Is400()
    {
        using var client = _factory.CreateClient();
        (await client.GetAsync(Me, Ct)).Dispose(); // has the antiforgery cookie, sends no header

        using var response = await client.PostAsJsonAsync(SignIn, new { email = BrowserClient.UniqueEmail(), password = Password }, Ct);

        await ShouldBeProblemAsync(response, HttpStatusCode.BadRequest, "auth.antiforgery_failed");
    }

    [Fact]
    public async Task UnsafeRequest_WithAnInvalidHeader_Is400()
    {
        using var client = _factory.CreateClient();
        (await client.GetAsync(Me, Ct)).Dispose();
        using var request = new HttpRequestMessage(HttpMethod.Post, SignOut);
        request.Headers.Add("X-XSRF-TOKEN", "not-a-token");

        using var response = await client.SendAsync(request, Ct);

        await ShouldBeProblemAsync(response, HttpStatusCode.BadRequest, "auth.antiforgery_failed");
    }

    [Fact]
    public async Task XsrfToken_FromBeforeSignIn_IsRejectedAfterIt()
    {
        using var client = _factory.CreateClient();
        using var first = await client.GetAsync(Me, Ct);
        var anonymousToken = Uri.UnescapeDataString(BrowserClient.SetCookie(first, "XSRF-TOKEN")!["XSRF-TOKEN=".Length..].Split(';')[0]);
        using var register = new HttpRequestMessage(HttpMethod.Post, Register)
        {
            Content = JsonContent.Create(new { email = BrowserClient.UniqueEmail(), password = Password, displayName = "Ada" }),
        };
        register.Headers.Add("X-XSRF-TOKEN", anonymousToken);
        (await client.SendAsync(register, Ct)).EnsureSuccessStatusCode();

        using var stale = new HttpRequestMessage(HttpMethod.Post, SignOut);
        stale.Headers.Add("X-XSRF-TOKEN", anonymousToken);
        using var response = await client.SendAsync(stale, Ct);

        await ShouldBeProblemAsync(response, HttpStatusCode.BadRequest, "auth.antiforgery_failed");
    }

    [Fact]
    public async Task Guest_IsSignedInForOneRoom_AndIsNotAUser()
    {
        // Guests' sessions skip Identity's security-stamp check, which would otherwise reject them on every request here.
        await using var factory = _factory.WithWebHostBuilder(b => b.ConfigureTestServices(
            s => s.Configure<SecurityStampValidatorOptions>(o => o.ValidationInterval = TimeSpan.Zero)));
        var cookie = await IssueGuestCookieAsync(factory, "Omar", "POP-428");
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("Cookie", cookie);

        var me = await client.GetFromJsonAsync<JsonElement>(Me, Ct);
        using var userOnly = await client.GetAsync(new Uri("/api/library/summary", UriKind.Relative), Ct);

        me.GetProperty("kind").GetString().ShouldBe("guest");
        me.GetProperty("displayName").GetString().ShouldBe("Omar");
        me.GetProperty("roomCode").GetString().ShouldBe("POP-428");
        me.GetProperty("email").ValueKind.ShouldBe(JsonValueKind.Null);
        await ShouldBeProblemAsync(userOnly, HttpStatusCode.Unauthorized, "auth.unauthenticated");
    }

    [Fact]
    public async Task User_SecurityStampIsStillChecked()
    {
        await using var factory = _factory.WithWebHostBuilder(b => b.ConfigureTestServices(
            s => s.Configure<SecurityStampValidatorOptions>(o => o.ValidationInterval = TimeSpan.Zero)));
        var client = await BrowserClient.RegisteredAsync(factory, Ct);
        (await client.GetAsync(Me, Ct)).StatusCode.ShouldBe(HttpStatusCode.OK);

        using (var scope = factory.Services.CreateScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<Infrastructure.Identity.ApplicationUser>>();
            var me = await client.GetFromJsonAsync<JsonElement>(Me, Ct);
            var user = await users.FindByIdAsync(me.GetProperty("id").GetString()!);
            await users.UpdateSecurityStampAsync(user!);
        }

        using var response = await client.GetAsync(Me, Ct);
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    private async Task<string> RegisterAsync()
    {
        var email = BrowserClient.UniqueEmail();
        var client = await BrowserClient.StartAsync(_factory, Ct);
        (await client.PostAsJsonAsync(Register, new { email, password = Password, displayName = "Ada" }, Ct)).EnsureSuccessStatusCode();
        return email;
    }

    /// <summary>The 401 body without its per-request traceId, for comparing failures.</summary>
    private static async Task<string> SignInProblemAsync(HttpClient client, string email, string password)
    {
        using var response = await client.PostAsJsonAsync(SignIn, new { email, password }, Ct);
        var problem = await ShouldBeProblemAsync(response, HttpStatusCode.Unauthorized, "auth.invalid_credentials");
        return string.Join('|', problem.EnumerateObject().Where(p => p.Name != "traceId").Select(p => $"{p.Name}={p.Value}"));
    }

    private static async Task<JsonElement> ShouldBeProblemAsync(HttpResponseMessage response, HttpStatusCode status, string code)
    {
        response.StatusCode.ShouldBe(status);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
        problem.GetProperty("code").GetString().ShouldBe(code);
        problem.TryGetProperty("traceId", out _).ShouldBeTrue();
        return problem;
    }

    /// <summary>What room join (BE-10) will do: create a guest and issue its cookie. Returns the Cookie header value.</summary>
    private static async Task<string> IssueGuestCookieAsync(WebApplicationFactory<Program> factory, string displayName, string roomCode)
    {
        using var scope = factory.Services.CreateScope();
        var services = scope.ServiceProvider;
        var guest = Guest.Create(displayName, DateTimeOffset.UtcNow);
        var db = services.GetRequiredService<AppDbContext>();
        db.Guests.Add(guest);
        await db.SaveChangesAsync(Ct);

        var http = new DefaultHttpContext { RequestServices = services };
        services.GetRequiredService<IHttpContextAccessor>().HttpContext = http;
        await services.GetRequiredService<ISessionManager>().SignInGuestAsync(guest.Id, Guid.NewGuid(), roomCode);

        var setCookie = http.Response.Headers.SetCookie.Single(c => c!.StartsWith("ithaca_session=", StringComparison.Ordinal))!;
        return setCookie.Split(';')[0];
    }
}
