using System.Net;
using System.Text.Json;
using Ithaca.Tests.Harness;

namespace Ithaca.Tests.Monitoring;

/// <summary>BE-21 (api-contract.md §1, ADR-0028): liveness always answers; readiness also needs the database.</summary>
[Collection(DatabaseCollectionDefinition.Name)]
public sealed class HealthEndpointTests(PostgresFixture postgres)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Ready_WithTheDatabaseUp_IsOk()
    {
        await using var factory = await IthacaApiFactory.StartAsync(postgres.ConnectionString);
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(new Uri("/api/health/ready", UriKind.Relative), Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync(Ct)).ShouldBe("{\"status\":\"ok\"}");
    }

    [Fact]
    public async Task Ready_WithTheDatabaseDown_IsUnavailable_WhileLivenessStillAnswers()
    {
        // Nothing listens on port 1: connecting fails at once. Not migrated on purpose (StartAsync would migrate).
        await using var factory = new IthacaApiFactory("Host=127.0.0.1;Port=1;Database=none;Username=none;Password=none;Timeout=2");
        using var client = factory.CreateClient();

        using var ready = await client.GetAsync(new Uri("/api/health/ready", UriKind.Relative), Ct);
        using var live = await client.GetAsync(new Uri("/api/health", UriKind.Relative), Ct);

        ready.StatusCode.ShouldBe(HttpStatusCode.ServiceUnavailable);
        using var problem = JsonDocument.Parse(await ready.Content.ReadAsStringAsync(Ct));
        problem.RootElement.GetProperty("code").GetString().ShouldBe("health.database_unavailable");
        live.StatusCode.ShouldBe(HttpStatusCode.OK);
    }
}
