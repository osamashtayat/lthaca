using Microsoft.EntityFrameworkCore;

namespace Ithaca.Infrastructure.Persistence;

/// <summary>The one place the provider, pgvector and naming conventions are configured, for runtime and design time alike.</summary>
internal static class PersistenceOptions
{
    public const string ConnectionStringName = "Ithaca";

    public static void Configure(DbContextOptionsBuilder options, string connectionString) =>
        options
            .UseNpgsql(connectionString, npgsql =>
            {
                npgsql.MigrationsAssembly(typeof(AppDbContext).Assembly.FullName);
                npgsql.UseVector();
            })
            .UseSnakeCaseNamingConvention();
}
