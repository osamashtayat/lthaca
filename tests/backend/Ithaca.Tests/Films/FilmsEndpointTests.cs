using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Ithaca.Application.Films;
using Ithaca.Tests.Harness;

namespace Ithaca.Tests.Films;

/// <summary>The reference slice end to end: HTTP → controller → service → EF/pgvector → DTO, and the error path.</summary>
[Collection(DatabaseCollectionDefinition.Name)]
public sealed class FilmsEndpointTests(PostgresFixture postgres) : IAsyncLifetime
{
    private IthacaApiFactory _factory = null!;

    public async ValueTask InitializeAsync() => _factory = await IthacaApiFactory.StartAsync(postgres.ConnectionString);

    public ValueTask DisposeAsync() => _factory.DisposeAsync();

    [Fact]
    public async Task Get_KnownTmdbId_ReturnsFilmDto()
    {
        var film = await _factory.CreateClient().GetFromJsonAsync<FilmDto>("/api/films/550", TestContext.Current.CancellationToken);

        film.ShouldNotBeNull();
        film.TmdbId.ShouldBe(550);
        film.Title.ShouldBe("Fight Club");
        film.Genres.ShouldBe(["Drama"]);
    }

    [Fact]
    public async Task Get_UnknownTmdbId_ReturnsProblemWithCode()
    {
        var response = await _factory.CreateClient().GetAsync(new Uri("/api/films/999999", UriKind.Relative), TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        body.RootElement.GetProperty("code").GetString().ShouldBe("film.not_found");
        body.RootElement.TryGetProperty("traceId", out _).ShouldBeTrue();
    }

    [Fact]
    public async Task Hub_Negotiate_Succeeds()
    {
        var response = await _factory.CreateClient().PostAsync(new Uri("/hubs/rooms/negotiate?negotiateVersion=1", UriKind.Relative), null, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }
}
