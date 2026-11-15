using Ithaca.Infrastructure.Persistence;
using Ithaca.Tests.Harness;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;

namespace Ithaca.Tests.Rooms;

/// <summary>
/// Issue #51: the <c>RoomMoodRetention</c> migration moves each room mood's link onto the mood, clears its text, deletes
/// orphaned room moods (a guest's or a user's), and leaves solo moods (those with a deck) alone. Runs on its own database in the shared container.
/// </summary>
[Collection(DatabaseCollectionDefinition.Name)]
public sealed class RoomMoodRetentionMigrationTests(PostgresFixture postgres)
{
    private const string Before = "20261114163927_RoomSwiping";
    private const string Target = "20261115161253_RoomMoodRetention";

    private const string RoomMood = "00000000-0000-0000-0000-0000000000b1";
    private const string Orphan = "00000000-0000-0000-0000-0000000000b2";
    private const string Solo = "00000000-0000-0000-0000-0000000000b3";
    private const string UserOrphan = "00000000-0000-0000-0000-0000000000b4";
    private const string Member = "00000000-0000-0000-0000-0000000000c1";

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Up_LinksRoomMoodsToTheirMember_ClearsTheirText_AndDropsOrphans()
    {
        var name = $"migration_{Guid.NewGuid():N}";
        await ExecuteAsync(postgres.ConnectionString, $"CREATE DATABASE {name};");
        var connectionString = new NpgsqlConnectionStringBuilder(postgres.ConnectionString) { Database = name }.ConnectionString;
        try
        {
            var options = new DbContextOptionsBuilder<AppDbContext>();
            PersistenceOptions.Configure(options, connectionString);
            await using var db = new AppDbContext(options.Options);
            var migrator = db.GetService<IMigrator>();
            await migrator.MigrateAsync(Before, Ct);

            // FK triggers are off for this session (the container user is a superuser): rows without their room/user/guest.
            await ExecuteAsync(connectionString, $$"""
                SET session_replication_role = replica;
                INSERT INTO mood_queries (id, user_id, moods, max_runtime, language, text, include_genres, exclude_genres,
                  max_runtime_minutes, min_release_year, max_release_year, keywords, text_ignored, created_at) VALUES
                  ('{{RoomMood}}', NULL, '{Laugh}', 'Any', NULL, 'a guest''s private words', '{Comedy}', '{}', NULL, NULL, NULL, '{}', false, now()),
                  ('{{Orphan}}', NULL, '{}', 'Any', NULL, 'left behind', '{}', '{}', NULL, NULL, NULL, '{}', false, now()),
                  ('{{Solo}}', '00000000-0000-0000-0000-0000000000d1', '{}', 'Any', NULL, 'my own deck', '{}', '{}', NULL, NULL, NULL, '{}', false, now()),
                  ('{{UserOrphan}}', '00000000-0000-0000-0000-0000000000d1', '{}', 'Any', NULL, 'a user''s old room words', '{}', '{}', NULL, NULL, NULL, '{}', false, now());
                INSERT INTO decks (id, user_id, room_id, source, mood_query_id, created_at) VALUES
                  ('00000000-0000-0000-0000-0000000000a9', '00000000-0000-0000-0000-0000000000d1', NULL, 'Mood', '{{Solo}}', now());
                INSERT INTO room_members (id, room_id, user_id, guest_id, seat, joined_at, left_at, hard_nos, mood_query_id) VALUES
                  ('{{Member}}', '00000000-0000-0000-0000-0000000000e1', NULL, '00000000-0000-0000-0000-0000000000f1', 1, now(), NULL, '{}', '{{RoomMood}}');
                """);

            await migrator.MigrateAsync(Target, Ct);

            var rows = await RowsAsync(connectionString);
            rows.Keys.Order().ShouldBe(new[] { RoomMood, Solo }.Order(), "orphaned room moods (a guest's and a user's) are gone");
            rows[RoomMood].ShouldBe((Member, null), "linked to its member, text cleared");
            rows[Solo].ShouldBe((null, "my own deck"), "solo moods keep their text");

            await ExecuteAsync(connectionString, $"DELETE FROM room_members WHERE id = '{Member}';");
            (await RowsAsync(connectionString)).Keys.ToList().ShouldBe(new List<string> { Solo }, "a room mood goes with its member");
        }
        finally
        {
            NpgsqlConnection.ClearAllPools();
            await ExecuteAsync(postgres.ConnectionString, $"DROP DATABASE IF EXISTS {name} WITH (FORCE);");
        }
    }

    private static async Task ExecuteAsync(string connectionString, string sql)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(Ct);
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync(Ct);
    }

    private static async Task<Dictionary<string, (string? Member, string? Text)>> RowsAsync(string connectionString)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(Ct);
        await using var command = new NpgsqlCommand("SELECT id::text, room_member_id::text, text FROM mood_queries", connection);
        await using var reader = await command.ExecuteReaderAsync(Ct);
        var rows = new Dictionary<string, (string?, string?)>();
        while (await reader.ReadAsync(Ct))
        {
            rows[reader.GetString(0)] = (reader.IsDBNull(1) ? null : reader.GetString(1), reader.IsDBNull(2) ? null : reader.GetString(2));
        }

        return rows;
    }
}
