using System.Net;
using System.Text;
using System.Text.Json;
using Ithaca.Tests.Harness;

namespace Ithaca.Tests.Contract;

/// <summary>
/// BE-01: every first-wave endpoint whose task hasn't landed answers 501 ProblemDetails <c>server.not_implemented</c>.
/// When a feature task implements an endpoint, it removes that row here and adds its own slice tests.
/// </summary>
[Collection(DatabaseCollectionDefinition.Name)]
public sealed class ContractStubEndpointTests(PostgresFixture postgres) : IAsyncLifetime
{
    private const string Deck = "/api/decks/0190a1b2-c3d4-7e5f-8a9b-0c1d2e3f4a5b";
    private const string Room = "/api/rooms/POP-428";
    private const string Item = "0190a1b2-c3d4-7e5f-8a9b-0c1d2e3f4a5c";
    private const string Op = "0190a1b2-c3d4-7e5f-8a9b-0c1d2e3f4a5d";

    private IthacaApiFactory _factory = null!;

    public async ValueTask InitializeAsync() => _factory = await IthacaApiFactory.StartAsync(postgres.ConnectionString);

    public ValueTask DisposeAsync() => _factory.DisposeAsync();

    /// <summary>Method, path, and a valid JSON body (so model validation passes and the stub answers).</summary>
    public static TheoryData<string, string, string?> Stubs => new()
    {
        { "GET", "/api/auth/me", null },
        { "POST", "/api/auth/register", """{"email":"ada@example.com","password":"correct horse","displayName":"Ada"}""" },
        { "POST", "/api/auth/sign-in", """{"email":"ada@example.com","password":"correct horse"}""" },
        { "POST", "/api/auth/sign-out", null },

        { "POST", "/api/decks", """{"source":"mood","mood":{"moods":["cozy"],"maxRuntime":"upTo2h","language":null,"text":null}}""" },
        { "GET", Deck, null },
        { "POST", $"{Deck}/items", null },
        { "POST", $"{Deck}/swipes", $$"""{"swipeId":"{{Op}}","deckItemId":"{{Item}}","direction":"up","seenVerdict":"notForMe"}""" },
        { "GET", $"{Deck}/top", null },

        { "POST", "/api/rooms", """{"name":"Friday Film Club","mode":"justSwipe","startsAt":null}""" },
        { "GET", $"{Room}/preview", null },
        { "POST", $"{Room}/join", """{"displayName":"Omar"}""" },
        { "GET", Room, null },
        { "PUT", $"{Room}/mood", """{"moods":["laugh"],"maxRuntime":"under90","hardNos":["horror"],"text":null}""" },
        { "POST", $"{Room}/start", null },
        { "GET", $"{Room}/deck?fromPosition=0", null },
        { "POST", $"{Room}/votes", $$"""{"voteId":"{{Op}}","deckItemId":"{{Item}}","direction":"right","seenVerdict":null}""" },
        { "GET", $"{Room}/progress", null },
        { "POST", $"{Room}/finish", null },
        { "GET", $"{Room}/result", null },
        { "POST", $"{Room}/leave", null },
        { "POST", $"{Room}/close", null },

        { "GET", "/api/library?shelf=watchlist&sort=title&page=1&pageSize=20", null },
        { "GET", "/api/library/summary", null },
        { "GET", "/api/library/550", null },
        { "PUT", "/api/library/550", """{"liked":true,"onWatchlist":false,"seen":true,"rating":4}""" },
        { "PUT", "/api/library/550/watchlist", null },
        { "DELETE", "/api/library/550/watchlist", null },

        { "GET", "/api/films?search=club&maxRuntime=under90&minVoteAverage=7.5&hideSeen=true&sort=rating", null },
        { "GET", "/api/films/genres", null },
        { "GET", "/api/films/550/trailer", null },
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

        using var response = await _factory.CreateClient().SendAsync(request, TestContext.Current.CancellationToken);

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
        using var response = await _factory.CreateClient().PostAsync(new Uri("/api/decks", UriKind.Relative), content, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }
}
