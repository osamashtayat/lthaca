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

    public ValueTask DisposeAsync() => _container.DisposeAsync();
}

[CollectionDefinition(Name)]
public sealed class DatabaseCollectionDefinition : ICollectionFixture<PostgresFixture>
{
    public const string Name = "database";
}
