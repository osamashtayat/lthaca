using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Ithaca.Tests.Harness;

namespace Ithaca.Tests.Taste;

/// <summary>
/// BE-14a: the taste contract (api-contract.md §12) before its logic. <c>GET /api/taste</c> is user-only; the new
/// <c>taste</c> deck source and room mode answer 501 until BE-14, while the other sources and modes keep working.
/// </summary>
[Collection(DatabaseCollectionDefinition.Name)]
public sealed class TasteContractTests(PostgresFixture postgres) : IAsyncLifetime
{
    private IthacaApiFactory _factory = null!;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync() => _factory = await IthacaApiFactory.StartAsync(postgres.ConnectionString);

    public ValueTask DisposeAsync() => _factory.DisposeAsync();

    [Fact]
    public async Task GetTaste_AnonymousOrGuest_IsUnauthenticated()
    {
        using var host = await BrowserClient.RegisteredAsync(_factory, Ct);
        using var created = await host.PostAsJsonAsync("/api/rooms", new { name = "Room", mode = "justSwipe", startsAt = (string?)null }, Ct);
        var code = (await created.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("code").GetString();
        using var guest = await BrowserClient.StartAsync(_factory, Ct);
        (await guest.PostAsJsonAsync($"/api/rooms/{code}/join", new { displayName = "Omar" }, Ct)).EnsureSuccessStatusCode();
        using var anonymous = await BrowserClient.StartAsync(_factory, Ct);

        foreach (var client in new[] { anonymous, guest })
        {
            using var response = await client.GetAsync("/api/taste", Ct);
            response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        }
    }

    [Fact]
    public async Task OtherSourcesAndModes_StillWork()
    {
        using var user = await BrowserClient.RegisteredAsync(_factory, Ct);

        using var deck = await user.PostAsJsonAsync("/api/decks", new { source = "justSwipe", mood = (object?)null }, Ct);
        using var room = await user.PostAsJsonAsync("/api/rooms", new { name = "Room", mode = "mood", startsAt = (string?)null }, Ct);

        deck.StatusCode.ShouldBe(HttpStatusCode.Created);
        room.StatusCode.ShouldBe(HttpStatusCode.Created);
    }
}
