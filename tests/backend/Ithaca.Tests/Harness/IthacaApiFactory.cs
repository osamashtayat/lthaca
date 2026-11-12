using Ithaca.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Ithaca.Tests.Harness;

/// <summary>
/// The real API in memory against a real pgvector database. Development environment, so DevDataSeeder adds the fake
/// TMDB films (550, 27205, 496243) once migrations ran.
/// </summary>
public sealed class IthacaApiFactory(string connectionString, IReadOnlyDictionary<string, string>? settings = null) : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.UseOfflineProviders();
        builder.UseSetting("ConnectionStrings:Ithaca", connectionString);
        foreach (var (key, value) in settings ?? new Dictionary<string, string>())
        {
            builder.UseSetting(key, value);
        }
    }

    /// <param name="settings">Extra configuration, e.g. <c>["CatalogSync:EmbedOverviews"] = "true"</c>.</param>
    public static async Task<IthacaApiFactory> StartAsync(string connectionString, IReadOnlyDictionary<string, string>? settings = null)
    {
        // Migrate before the host starts, so the dev seeder finds the table.
        var options = new DbContextOptionsBuilder<AppDbContext>();
        PersistenceOptions.Configure(options, connectionString);
        await using (var db = new AppDbContext(options.Options))
        {
            await db.Database.MigrateAsync();
        }

        var factory = new IthacaApiFactory(connectionString, settings);
        _ = factory.Services.GetRequiredService<IServiceProvider>(); // starts the host (runs the seeder)
        return factory;
    }
}
