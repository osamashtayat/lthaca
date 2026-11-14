using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Ithaca.Tests.Harness;

namespace Ithaca.Tests.Users;

/// <summary>
/// BE-16a: the profile contract (api-contract.md §14) before its logic. The four actions are user-only and answer 501
/// until BE-16; password change and deletion issue a fresh <c>XSRF-TOKEN</c>; deletion takes its password in a DELETE body.
/// </summary>
[Collection(DatabaseCollectionDefinition.Name)]
public sealed class ProfileContractTests(PostgresFixture postgres) : IAsyncLifetime
{
    private IthacaApiFactory _factory = null!;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync() => _factory = await IthacaApiFactory.StartAsync(postgres.ConnectionString);

    public ValueTask DisposeAsync() => _factory.DisposeAsync();

    /// <summary>Method, path and a valid JSON body.</summary>
    public static TheoryData<string, string, string?> Actions => new()
    {
        { "GET", "/api/profile", null },
        { "PUT", "/api/profile", """{"displayName":"Lina"}""" },
        { "PUT", "/api/profile/password", """{"currentPassword":"correct horse","newPassword":"battery staple"}""" },
        { "DELETE", "/api/profile", """{"password":"correct horse"}""" },
    };

    [Theory]
    [MemberData(nameof(Actions))]
    public async Task Action_SignedInUser_ReachesTheStub(string method, string path, string? body)
    {
        using var user = await BrowserClient.RegisteredAsync(_factory, Ct);

        using var response = await user.SendAsync(Request(method, path, body), Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.NotImplemented, await response.Content.ReadAsStringAsync(Ct));
        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");
        (await CodeAsync(response)).ShouldBe("server.not_implemented");
    }

    [Theory]
    [MemberData(nameof(Actions))]
    public async Task Action_GuestOrAnonymous_IsUnauthenticated(string method, string path, string? body)
    {
        using var host = await BrowserClient.RegisteredAsync(_factory, Ct);
        using var created = await host.PostAsJsonAsync("/api/rooms", new { name = "Room", mode = "justSwipe", startsAt = (string?)null }, Ct);
        var code = (await created.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("code").GetString();
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
    [InlineData("PUT", "/api/profile/password", """{"currentPassword":"","newPassword":"battery staple"}""", "currentPassword")]
    [InlineData("DELETE", "/api/profile", """{"password":""}""", "password")]
    public async Task Action_InvalidBody_IsValidationErrorOnTheField(string method, string path, string body, string field)
    {
        using var user = await BrowserClient.RegisteredAsync(_factory, Ct);

        using var response = await user.SendAsync(Request(method, path, body), Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        using var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct));
        problem.RootElement.GetProperty("code").GetString().ShouldBe("validation");
        problem.RootElement.GetProperty("errors").EnumerateObject().Select(p => p.Name).ShouldContain(field, problem.RootElement.ToString());
    }

    [Theory]
    [InlineData("PUT", "/api/profile/password", """{"currentPassword":"correct horse","newPassword":"battery staple"}""")]
    [InlineData("DELETE", "/api/profile", """{"password":"correct horse"}""")]
    public async Task SessionChangingAction_IssuesAFreshXsrfToken(string method, string path, string body)
    {
        using var user = await BrowserClient.RegisteredAsync(_factory, Ct);

        using var response = await user.SendAsync(Request(method, path, body), Ct);

        BrowserClient.SetCookie(response, "XSRF-TOKEN").ShouldNotBeNull();
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
}
