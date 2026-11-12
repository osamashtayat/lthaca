using Ithaca.Application.Ai;
using Ithaca.Application.Common;
using Ithaca.Application.Tmdb;
using Ithaca.Infrastructure.Ai;
using Ithaca.Infrastructure.Configuration;
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

        services.AddExternalClients(configuration);

        services.AddValidatedOptions<CatalogSyncOptions>(configuration);
        services.AddHostedService<CatalogSyncJob>();
        return services;
    }

    /// <summary>
    /// TMDB, LLM and embedding clients, each picked by config. Only the Fake implementations exist yet; selecting a real
    /// provider fails at startup with a clear message until its client is built (docs/ai.md, "Adding a provider").
    /// </summary>
    private static void AddExternalClients(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddValidatedOptions<AiOptions>(configuration)
            .Validate(o => o.LlmProvider == LlmProvider.Fake, "Ai:LlmProvider: only 'Fake' is implemented so far.")
            .Validate(o => o.EmbeddingProvider == EmbeddingProvider.Fake, "Ai:EmbeddingProvider: only 'Fake' is implemented so far.");
        services.AddValidatedOptions<TmdbOptions>(configuration)
            .Validate(o => o.Client == TmdbClientKind.Fake, "Tmdb:Client: only 'Fake' is implemented so far.");

        services.AddSingleton<IPromptLibrary, FilePromptLibrary>();
        services.AddSingleton<ILlmClient>(sp => sp.GetRequiredService<IOptions<AiOptions>>().Value.LlmProvider switch
        {
            LlmProvider.Fake => new FakeLlmClient(),
            var other => throw NotImplemented("LLM", other),
        });
        services.AddSingleton<IEmbeddingClient>(sp => sp.GetRequiredService<IOptions<AiOptions>>().Value.EmbeddingProvider switch
        {
            EmbeddingProvider.Fake => new FakeEmbeddingClient(),
            var other => throw NotImplemented("embedding", other),
        });
        services.AddSingleton<ITmdbClient>(sp => sp.GetRequiredService<IOptions<TmdbOptions>>().Value.Client switch
        {
            TmdbClientKind.Fake => new FakeTmdbClient(),
            var other => throw NotImplemented("TMDB", other),
        });
    }

    private static NotSupportedException NotImplemented(string kind, Enum provider) =>
        new($"The {kind} client '{provider}' is not implemented yet.");
}
