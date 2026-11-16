using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Ithaca.Tests.Harness;

namespace Ithaca.Tests.Users;

/// <summary>
/// BE-18a: the password reset contract (api-contract.md §4): both actions are open to anyone with an XSRF token; input is
/// validated first; only the reset (which signs in) issues a fresh token. The behaviour is in <see cref="PasswordResetTests"/>.
/// </summary>
[Collection(DatabaseCollectionDefinition.Name)]
public sealed class PasswordResetContractTests(PostgresFixture postgres) : IAsyncLifetime
{
    private const string ForgotPassword = "/api/auth/forgot-password";
    private const string ResetPassword = "/api/auth/reset-password";

    private IthacaApiFactory _factory = null!;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync() => _factory = await IthacaApiFactory.StartAsync(postgres.ConnectionString);

    public ValueTask DisposeAsync() => _factory.DisposeAsync();

    /// <summary>Path and a valid JSON body.</summary>
    public static TheoryData<string, string> Actions => new()
    {
        { ForgotPassword, """{"email":"someone@example.com"}""" },
        { ResetPassword, """{"token":"opaque-token","newPassword":"battery staple"}""" },
    };

    [Theory]
    [InlineData(ForgotPassword, """{"email":""}""", "email")]
    [InlineData(ForgotPassword, """{"email":"not an email"}""", "email")]
    [InlineData(ResetPassword, """{"token":"","newPassword":"battery staple"}""", "token")]
    [InlineData(ResetPassword, """{"token":"opaque-token","newPassword":"short"}""", "newPassword")]
    public async Task Action_InvalidBody_IsValidationErrorOnTheField(string path, string body, string field)
    {
        using var visitor = await BrowserClient.StartAsync(_factory, Ct);

        using var response = await visitor.SendAsync(Post(path, body), Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        using var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct));
        problem.RootElement.GetProperty("code").GetString().ShouldBe("validation");
        problem.RootElement.GetProperty("errors").EnumerateObject().Select(p => p.Name).ShouldContain(field, problem.RootElement.ToString());
    }

    [Fact]
    public async Task Reset_IssuesAFreshXsrfToken_ButForgotDoesNot()
    {
        using var visitor = await BrowserClient.StartAsync(_factory, Ct);

        using var reset = await visitor.SendAsync(Post(ResetPassword, """{"token":"opaque-token","newPassword":"battery staple"}"""), Ct);
        using var forgot = await visitor.SendAsync(Post(ForgotPassword, """{"email":"someone@example.com"}"""), Ct);

        BrowserClient.SetCookie(reset, "XSRF-TOKEN").ShouldNotBeNull();
        BrowserClient.SetCookie(forgot, "XSRF-TOKEN").ShouldBeNull();
    }

    [Theory]
    [MemberData(nameof(Actions))]
    public async Task Action_WithoutXsrfHeader_IsRefused(string path, string body)
    {
        using var plain = _factory.CreateClient();

        using var response = await plain.SendAsync(Post(path, body), Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await CodeAsync(response)).ShouldBe("auth.antiforgery_failed");
    }

    private static HttpRequestMessage Post(string path, string body) =>
        new(HttpMethod.Post, new Uri(path, UriKind.Relative)) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    private static async Task<string?> CodeAsync(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("code").GetString();
}
