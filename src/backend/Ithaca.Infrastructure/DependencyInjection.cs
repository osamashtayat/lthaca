using Anthropic;
using Ithaca.Application.Ai;
using Ithaca.Application.Catalog;
using Ithaca.Application.Common;
using Ithaca.Application.Tmdb;
using Ithaca.Infrastructure.Ai;
using Ithaca.Infrastructure.Configuration;
using Ithaca.Infrastructure.Identity;
using Ithaca.Infrastructure.Jobs;
using Ithaca.Infrastructure.Persistence;
using Ithaca.Infrastructure.Tmdb;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Ithaca.Infrastructure;

public static class DependencyInjection
{
    /// <summary>
    /// Registers persistence, external clients (chosen by config) and background jobs. The connection string comes from
    /// <c>ConnectionStrings__Ithaca</c> (env var or user-secrets), never from committed appsettings. It is read when a
    /// context is first created, so the app starts (and /api/health answers) without one.
    /// </summary>
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<AppDbContext>(options =>
        {
            var connectionString = configuration.GetConnectionString(PersistenceOptions.ConnectionStringName);
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                throw new InvalidOperationException(
                    "Connection string 'Ithaca' is not configured. Set ConnectionStrings__Ithaca or use dotnet user-secrets.");
            }

            PersistenceOptions.Configure(options, connectionString);
        });
        services.AddScoped<IAppDbContext>(sp => sp.GetRequiredService<AppDbContext>());
        services.AddIthacaIdentity();

        services.AddExternalClients(configuration);

        // CatalogSyncOptions lives with its consumer (Application) but is bound here: AddApplication takes no configuration.
        services.AddValidatedOptions<CatalogSyncOptions>(configuration);
        services.AddHostedService<CatalogSyncJob>();
        return services;
    }

    /// <summary>
    /// TMDB, LLM and embedding clients, each picked by config (docs/ai.md). Built: Fake everywhere, Anthropic (LLM), Http (TMDB).
    /// A real provider without its key, or a provider that isn't built, fails at startup with a clear message.
    /// </summary>
    private static void AddExternalClients(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddValidatedOptions<AiOptions>(configuration)
            .Validate(o => o.LlmProvider is LlmProvider.Fake or LlmProvider.Anthropic, "Ai:LlmProvider: only 'Fake' and 'Anthropic' are implemented.")
            .Validate(o => o.LlmProvider != LlmProvider.Anthropic || !string.IsNullOrWhiteSpace(o.ApiKey), "Ai:ApiKey is required when Ai:LlmProvider is 'Anthropic' (user-secrets or Ai__ApiKey).")
            .Validate(o => o.EmbeddingProvider == EmbeddingProvider.Fake, "Ai:EmbeddingProvider: only 'Fake' is implemented so far (Voyage: ADR-0013).");
        services.AddValidatedOptions<TmdbOptions>(configuration)
            .Validate(o => o.Client != TmdbClientKind.Http || !string.IsNullOrWhiteSpace(o.ApiKey), "Tmdb:ApiKey is required when Tmdb:Client is 'Http' (user-secrets or Tmdb__ApiKey).");

        services.AddSingleton<IPromptLibrary, FilePromptLibrary>();

        // The SDK client is created only when it's used, so Fake setups need no key.
        services.AddSingleton(sp => new AnthropicClient { ApiKey = sp.GetRequiredService<IOptions<AiOptions>>().Value.ApiKey });
        services.AddSingleton<AnthropicLlmClient>();
        services.AddSingleton<ILlmClient>(sp => sp.GetRequiredService<IOptions<AiOptions>>().Value.LlmProvider switch
        {
            LlmProvider.Fake => new FakeLlmClient(),
            LlmProvider.Anthropic => sp.GetRequiredService<AnthropicLlmClient>(),
            var other => throw NotImplemented("LLM", other),
        });

        services.AddSingleton<IEmbeddingClient>(sp => sp.GetRequiredService<IOptions<AiOptions>>().Value.EmbeddingProvider switch
        {
            EmbeddingProvider.Fake => new FakeEmbeddingClient(),
            var other => throw NotImplemented("embedding", other),
        });

        services.AddHttpClient<TmdbHttpClient>((sp, http) =>
        {
            http.BaseAddress = new Uri(sp.GetRequiredService<IOptions<TmdbOptions>>().Value.BaseUrl);
            http.Timeout = TimeSpan.FromSeconds(20);
        });
        services.AddTransient<ITmdbClient>(sp => sp.GetRequiredService<IOptions<TmdbOptions>>().Value.Client switch
        {
            TmdbClientKind.Fake => new FakeTmdbClient(),
            TmdbClientKind.Http => sp.GetRequiredService<TmdbHttpClient>(),
            var other => throw NotImplemented("TMDB", other),
        });
    }

    private static NotSupportedException NotImplemented(string kind, Enum provider) =>
        new($"The {kind} client '{provider}' is not implemented yet.");
}
