using Npgsql;
using Testcontainers.PostgreSql;

namespace Ithaca.Tests.Harness;

/// <summary>
/// A throwaway PostgreSQL + pgvector container (same image as docker-compose) shared by one test collection.
/// Needs Docker. Migrations are applied by <see cref="IthacaApiFactory"/>.
/// </summary>
public sealed class PostgresFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("pgvector/pgvector:pg17").Build();

    public string ConnectionString => _container.GetConnectionString();

    public ValueTask InitializeAsync() => new(_container.StartAsync());

    /// <summary>
    /// A new, empty database on the same server, for tests whose assertions depend on what else is in the catalog (e.g.
    /// which films an import adds). <see cref="IthacaApiFactory"/> migrates it like the shared one.
    /// </summary>
    public async Task<string> CreateDatabaseAsync(CancellationToken ct)
    {
        var name = $"t_{Guid.NewGuid():N}";
        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync(ct);
        await using var create = new NpgsqlCommand($"CREATE DATABASE \"{name}\"", connection);
        await create.ExecuteNonQueryAsync(ct);
        return new NpgsqlConnectionStringBuilder(ConnectionString) { Database = name }.ConnectionString;
    }

    public ValueTask DisposeAsync() => _container.DisposeAsync();
}

[CollectionDefinition(Name)]
public sealed class DatabaseCollectionDefinition : ICollectionFixture<PostgresFixture>
{
    public const string Name = "database";
}
