using System.Text.Json.Nodes;
using Ithaca.Tests.Harness;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Ithaca.Tests.Contract;

/// <summary>
/// contracts/openapi.json (the frontend's type source) must equal what the code serves.
/// Fix a failure with scripts/export-openapi.sh, then regenerate frontend types (npm run api:types).
/// </summary>
public sealed class OpenApiContractTests
{
    [Fact]
    public async Task CommittedContract_MatchesTheApi()
    {
        await using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(b => b.UseEnvironment("Development"));
        var served = JsonNode.Parse(await factory.CreateClient().GetStringAsync(new Uri("/openapi/v1.json", UriKind.Relative), TestContext.Current.CancellationToken));
        var committed = JsonNode.Parse(await File.ReadAllTextAsync(RepoPaths.File("contracts", "openapi.json"), TestContext.Current.CancellationToken));

        JsonNode.DeepEquals(served, committed).ShouldBeTrue("contracts/openapi.json is stale: run scripts/export-openapi.sh");
    }
}
