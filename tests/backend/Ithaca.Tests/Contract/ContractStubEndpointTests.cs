using System.Net;
using System.Text;
using System.Text.Json;
using Ithaca.Tests.Harness;

namespace Ithaca.Tests.Contract;

/// <summary>
/// BE-01: every first-wave endpoint whose task hasn't landed answers 501 ProblemDetails <c>server.not_implemented</c>.
/// When a feature task implements an endpoint, it removes that row here and adds its own slice tests. Requests come from
/// a signed-in browser client (session cookie + XSRF header, BE-02), so access rules don't hide the stub.
/// </summary>
[Collection(DatabaseCollectionDefinition.Name)]
public sealed class ContractStubEndpointTests(PostgresFixture postgres) : IAsyncLifetime
{
    private const string Deck = "/api/decks/0190a1b2-c3d4-7e5f-8a9b-0c1d2e3f4a5b";
    private const string Room = "/api/rooms/POP-428";
    private const string Item = "0190a1b2-c3d4-7e5f-8a9b-0c1d2e3f4a5c";
    private const string Op = "0190a1b2-c3d4-7e5f-8a9b-0c1d2e3f4a5d";

    private IthacaApiFactory _factory = null!;
    private HttpClient _user = null!;

    public async ValueTask InitializeAsync()
    {
        _factory = await IthacaApiFactory.StartAsync(postgres.ConnectionString);
        _user = await BrowserClient.RegisteredAsync(_factory, TestContext.Current.CancellationToken);
    }

    public ValueTask DisposeAsync()
    {
        _user.Dispose();
        return _factory.DisposeAsync();
    }

    /// <summary>Method, path, and a valid JSON body (so model validation passes and the stub answers).</summary>
    public static TheoryData<string, string, string?> Stubs => new()
    {
        { "PUT", $"{Room}/mood", """{"moods":["laugh"],"maxRuntime":"under90","hardNos":["horror"],"text":null}""" },
        { "POST", $"{Room}/start", null },
        { "GET", $"{Room}/deck?fromPosition=0", null },
        { "POST", $"{Room}/votes", $$"""{"voteId":"{{Op}}","deckItemId":"{{Item}}","direction":"right","seenVerdict":null}""" },
        { "GET", $"{Room}/progress", null },
        { "POST", $"{Room}/finish", null },
        { "GET", $"{Room}/result", null },
        { "GET", "/api/taste", null },
        { "GET", "/api/imports", null },
        { "GET", $"/api/imports/{Op}", null },
        { "GET", $"/api/imports/{Op}/not-found?page=1&pageSize=20", null },
        { "POST", "/api/decks", """{"source":"taste","mood":null}""" },
        { "POST", "/api/rooms", """{"name":"Blend night","mode":"taste","startsAt":null}""" },
    };

    [Theory]
    [MemberData(nameof(Stubs))]
    public async Task Stub_AnswersNotImplementedProblem(string method, string path, string? body)
    {
        using var request = new HttpRequestMessage(new HttpMethod(method), new Uri(path, UriKind.Relative));
        if (body is not null)
        {
            request.Content = new StringContent(body, Encoding.UTF8, "application/json");
        }

        using var response = await _user.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.NotImplemented, await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");
        using var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        problem.RootElement.GetProperty("code").GetString().ShouldBe("server.not_implemented");
    }

    [Fact]
    public async Task ListQuery_InvalidPageSize_ReportsTheCamelCaseField()
    {
        using var response = await _factory.CreateClient().GetAsync(new Uri("/api/films?pageSize=0", UriKind.Relative), TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        using var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        problem.RootElement.GetProperty("code").GetString().ShouldBe("validation");
        problem.RootElement.GetProperty("errors").TryGetProperty("pageSize", out _).ShouldBeTrue();
    }

    [Fact]
    public async Task EnumAsInteger_IsRejected()
    {
        using var content = new StringContent("""{"source":0,"mood":null}""", Encoding.UTF8, "application/json");
        using var response = await _user.PostAsync(new Uri("/api/decks", UriKind.Relative), content, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task RequestWithoutARequiredField_IsRejected()
    {
        // No "direction": must be a 400, never a silent default ("right" would be an unintended yes).
        using var content = new StringContent($$"""{"swipeId":"{{Op}}","deckItemId":"{{Item}}","seenVerdict":null}""", Encoding.UTF8, "application/json");
        using var response = await _user.PostAsync(new Uri($"{Deck}/swipes", UriKind.Relative), content, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }
}
