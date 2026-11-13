using System.Net;
using System.Text;
using Ithaca.Application.Wikidata;
using Ithaca.Infrastructure;
using Ithaca.Infrastructure.Wikidata;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace Ithaca.Tests.Wikidata;

/// <summary>
/// Wikidata's SPARQL endpoint against a stub handler. The recorded reply is the real endpoint's answer for TMDB ids 550,
/// 27205 and 999999999 (unknown), captured 2026-11-13 with this client's query. No network.
/// </summary>
public sealed class WikidataHttpClientTests
{
    private static readonly string RecordedReply =
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Wikidata", "Recorded", "film-facts-550-27205-unknown.json"));

    private static (WikidataHttpClient Client, StubHandler Handler) Create(HttpStatusCode status = HttpStatusCode.OK, string? body = null)
    {
        var handler = new StubHandler(status, body ?? RecordedReply);
        var http = new HttpClient(handler) { BaseAddress = new Uri("https://query.wikidata.org/sparql") };
        return (new WikidataHttpClient(http, NullLogger<WikidataHttpClient>.Instance), handler);
    }

    [Fact]
    public async Task GetFilms_PostsOneSparqlQueryForTheBatch()
    {
        var (client, handler) = Create();

        await client.GetFilmsAsync([550, 27205, 999999999], TestContext.Current.CancellationToken);

        handler.Requests.ShouldBe(1);
        handler.LastRequest!.Method.ShouldBe(HttpMethod.Post);
        handler.LastRequest.RequestUri.ShouldBe(new Uri("https://query.wikidata.org/sparql"));
        handler.LastRequest.Headers.Accept.ShouldContain(h => h.MediaType == "application/sparql-results+json");
        handler.LastBody!.ShouldStartWith("query=");
        var query = WebUtility.UrlDecode(handler.LastBody["query=".Length..]);
        query.ShouldContain("""VALUES ?tmdb { "550" "27205" "999999999" }""");
        query.ShouldContain("wdt:P4947", customMessage: "films are linked by Wikidata's TMDB movie id, never by title");
    }

    [Fact]
    public async Task GetFilms_MapsTheRecordedReply()
    {
        var (client, _) = Create();

        var result = await client.GetFilmsAsync([550, 27205, 999999999], TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Select(f => f.TmdbId).ShouldBe([550, 27205], "the unknown id is simply absent");
        var fightClub = result.Value[0].Facts;
        fightClub.WikidataId.ShouldBe("Q190050");
        fightClub.Directors.ShouldBe(["David Fincher"]);
        fightClub.Cast.Take(3).ShouldBe(["Brad Pitt", "Edward Norton", "Jared Leto"], "best-known first (sitelinks)");
        fightClub.Genres.ShouldContain("psychological thriller");
        fightClub.MainSubjects.ShouldBe(["alter ego"]);
        fightClub.Languages.ShouldBe(["English"]);
        fightClub.ReleaseYear.ShouldBe(1999, "the earliest of several release dates");
        fightClub.RuntimeMinutes.ShouldBe(139, "8340 seconds");
        var inception = result.Value[1].Facts;
        inception.WikidataId.ShouldBe("Q25188");
        inception.Awards.ShouldContain("Academy Award for Best Visual Effects");
        inception.RuntimeMinutes.ShouldBe(148);
    }

    [Fact]
    public async Task GetFilms_TwoItemsForOneTmdbId_KeepsTheOldest()
    {
        const string body = """
            {"results":{"bindings":[
              {"tmdb":{"value":"7"},"item":{"value":"http://www.wikidata.org/entity/Q900"},"kind":{"value":"genre"},"label":{"value":"remake"}},
              {"tmdb":{"value":"7"},"item":{"value":"http://www.wikidata.org/entity/Q12"},"kind":{"value":"genre"},"label":{"value":"original"}}
            ]}}
            """;
        var (client, _) = Create(body: body);

        var result = await client.GetFilmsAsync([7], TestContext.Current.CancellationToken);

        result.Value.ShouldHaveSingleItem().Facts.ShouldSatisfyAllConditions(
            f => f.WikidataId.ShouldBe("Q12"),
            f => f.Genres.ShouldBe(["original"]));
    }

    [Fact]
    public async Task GetFilms_NoIds_SendsNothing()
    {
        var (client, handler) = Create();

        var result = await client.GetFilmsAsync([], TestContext.Current.CancellationToken);

        result.Value.ShouldBeEmpty();
        handler.Requests.ShouldBe(0);
    }

    [Theory]
    [InlineData(HttpStatusCode.TooManyRequests)]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.GatewayTimeout)]
    public async Task FailureStatus_IsUnavailable(HttpStatusCode status)
    {
        var (client, _) = Create(status, "{}");

        var result = await client.GetFilmsAsync([550], TestContext.Current.CancellationToken);

        result.Error.ShouldBe(WikidataErrors.Unavailable);
    }

    [Theory]
    [InlineData("<html>busy</html>")]
    [InlineData("{}")]
    public async Task UnreadableBody_IsUnavailable(string body)
    {
        var (client, _) = Create(body: body);

        var result = await client.GetFilmsAsync([550], TestContext.Current.CancellationToken);

        result.Error.ShouldBe(WikidataErrors.Unavailable);
    }

    [Fact]
    public async Task Timeout_IsUnavailable()
    {
        var handler = new StubHandler(HttpStatusCode.OK, "{}") { Throw = new TaskCanceledException("HttpClient.Timeout") };
        var client = new WikidataHttpClient(
            new HttpClient(handler) { BaseAddress = new Uri("https://query.wikidata.org/sparql") }, NullLogger<WikidataHttpClient>.Instance);

        var result = await client.GetFilmsAsync([550], TestContext.Current.CancellationToken);

        result.Error.ShouldBe(WikidataErrors.Unavailable);
    }

    [Fact]
    public void Registration_SendsADescriptiveUserAgentWithAProjectContact()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Wikidata:Client"] = "Http",
        }).Build();
        using var services = new ServiceCollection().AddLogging().AddInfrastructure(configuration).BuildServiceProvider();

        var http = services.GetRequiredService<IHttpClientFactory>().CreateClient(nameof(WikidataHttpClient));
        var userAgent = http.DefaultRequestHeaders.UserAgent.ToString();

        userAgent.ShouldStartWith("IthacaCatalogBot/");
        userAgent.ShouldContain("https://github.com/");
        userAgent.ShouldNotContain("@", customMessage: "a project contact, never a personal email");
        http.BaseAddress.ShouldBe(new Uri("https://query.wikidata.org/sparql"));
        services.GetRequiredService<IWikidataClient>().ShouldBeOfType<WikidataHttpClient>();
    }

    private sealed class StubHandler(HttpStatusCode status, string body) : HttpMessageHandler
    {
        public HttpRequestMessage? LastRequest { get; private set; }

        public string? LastBody { get; private set; }

        public int Requests { get; private set; }

        public Exception? Throw { get; init; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests++;
            LastRequest = request;
            LastBody = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
            return Throw is not null
                ? throw Throw
                : new HttpResponseMessage(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
        }
    }
}
