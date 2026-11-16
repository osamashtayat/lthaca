using Ithaca.Infrastructure.Persistence;
using Ithaca.Tests.Harness;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;

namespace Ithaca.Tests.Library;

/// <summary>
/// BE-20: the <c>HalfStarRatings</c> migration converts existing whole-star ratings (× 2) into <c>rating_half_stars</c>, and
/// its Down rounds half stars up. Runs on its own database in the shared container, so other tests never see it.
/// </summary>
[Collection(DatabaseCollectionDefinition.Name)]
public sealed class HalfStarRatingsMigrationTests(PostgresFixture postgres)
{
    private const string Before = "20261113203911_RoomsAndMembers";
    private const string Target = "20261114112359_HalfStarRatings";

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Up_DoublesExistingRatings_AndDown_RoundsHalfStarsUp()
    {
        var (name, connectionString) = await NewDatabaseAsync();
        try
        {
            await RunAsync(connectionString);
        }
        finally
        {
            NpgsqlConnection.ClearAllPools();
            await using var admin = await OpenAsync(postgres.ConnectionString);
            await ExecuteAsync(admin, $"DROP DATABASE IF EXISTS {name} WITH (FORCE);");
        }
    }

    private static async Task RunAsync(string connectionString)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>();
        PersistenceOptions.Configure(options, connectionString);
        await using var db = new AppDbContext(options.Options);
        var migrator = db.GetService<IMigrator>();

        await migrator.MigrateAsync(Before, Ct);
        await using (var connection = await OpenAsync(connectionString))
        {
            // Rows without their user/film: FK triggers are off for this session (the container user is a superuser).
            await ExecuteAsync(connection, """
                SET session_replication_role = replica;
                INSERT INTO user_films (user_id, film_id, liked, on_watchlist, seen, rating, created_at, updated_at) VALUES
                  ('00000000-0000-0000-0000-000000000001', '00000000-0000-0000-0000-0000000000a1', false, false, true, 1, now(), now()),
                  ('00000000-0000-0000-0000-000000000001', '00000000-0000-0000-0000-0000000000a2', false, false, true, 4, now(), now()),
                  ('00000000-0000-0000-0000-000000000001', '00000000-0000-0000-0000-0000000000a3', true, false, false, NULL, now(), now());
                """);
        }

        await migrator.MigrateAsync(Target, Ct);
        await using (var connection = await OpenAsync(connectionString))
        {
            (await HalfStarsAsync(connection)).ShouldBe([2, 8, null], "whole stars × 2; unrated stays null");
            await ExecuteAsync(connection, "UPDATE user_films SET rating_half_stars = 7 WHERE film_id = '00000000-0000-0000-0000-0000000000a2';");
            await Should.ThrowAsync<PostgresException>(() => ExecuteAsync(
                connection, "UPDATE user_films SET rating_half_stars = 11 WHERE film_id = '00000000-0000-0000-0000-0000000000a1';"));
        }

        await migrator.MigrateAsync(Before, Ct);
        await using (var connection = await OpenAsync(connectionString))
        {
            await using var command = new NpgsqlCommand("SELECT rating FROM user_films ORDER BY film_id", connection);
            await using var reader = await command.ExecuteReaderAsync(Ct);
            var ratings = new List<int?>();
            while (await reader.ReadAsync(Ct))
            {
                ratings.Add(reader.IsDBNull(0) ? null : reader.GetInt32(0));
            }

            ratings.ShouldBe([1, 4, null], "Down: 1 → 1, 3.5 → 4");
        }
    }

    private async Task<(string Name, string ConnectionString)> NewDatabaseAsync()
    {
        var name = $"migration_{Guid.NewGuid():N}";
        await using (var admin = await OpenAsync(postgres.ConnectionString))
        {
            await ExecuteAsync(admin, $"CREATE DATABASE {name};");
        }

        return (name, new NpgsqlConnectionStringBuilder(postgres.ConnectionString) { Database = name }.ConnectionString);
    }

    private static async Task<NpgsqlConnection> OpenAsync(string connectionString)
    {
        var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(Ct);
        return connection;
    }

    private static async Task ExecuteAsync(NpgsqlConnection connection, string sql)
    {
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync(Ct);
    }

    private static async Task<List<short?>> HalfStarsAsync(NpgsqlConnection connection)
    {
        await using var command = new NpgsqlCommand("SELECT rating_half_stars FROM user_films ORDER BY film_id", connection);
        await using var reader = await command.ExecuteReaderAsync(Ct);
        var values = new List<short?>();
        while (await reader.ReadAsync(Ct))
        {
            values.Add(reader.IsDBNull(0) ? null : reader.GetInt16(0));
        }

        return values;
    }
}
