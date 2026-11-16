using System.Net;
using System.Text;
using System.Text.Json;
using Ithaca.Tests.Harness;

namespace Ithaca.Tests.Contract;

/// <summary>
/// BE-01: request validation at the contract's edge (camelCase field names, enums as strings, required fields). Requests
/// come from a signed-in browser client (session cookie + XSRF header, BE-02), so access rules don't hide the answer.
/// </summary>
[Collection(DatabaseCollectionDefinition.Name)]
public sealed class ContractRequestValidationTests(PostgresFixture postgres) : IAsyncLifetime
{
    private const string Deck = "/api/decks/0190a1b2-c3d4-7e5f-8a9b-0c1d2e3f4a5b";
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
