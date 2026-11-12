using Microsoft.AspNetCore.Hosting;

namespace Ithaca.Tests.Harness;

/// <summary>
/// Test hosts run in Development, which also loads the developer's user-secrets (real keys, real providers). Every test host
/// applies these settings so tests stay offline and deterministic whatever the machine has configured.
/// </summary>
internal static class OfflineProviders
{
    private static readonly Dictionary<string, string> Settings = new(StringComparer.Ordinal)
    {
        ["Ai:LlmProvider"] = "Fake",
        ["Ai:EmbeddingProvider"] = "Fake",
        ["Tmdb:Client"] = "Fake",
        ["CatalogSync:Enabled"] = "false",
    };

    public static IWebHostBuilder UseOfflineProviders(this IWebHostBuilder builder)
    {
        foreach (var (key, value) in Settings)
        {
            builder.UseSetting(key, value);
        }

        return builder;
    }
}
