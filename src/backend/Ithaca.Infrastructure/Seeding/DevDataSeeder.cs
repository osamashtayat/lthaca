using Ithaca.Domain.Films;
using Ithaca.Infrastructure.Persistence;
using Ithaca.Infrastructure.Tmdb;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace Ithaca.Infrastructure.Seeding;

/// <summary>
/// Development only: puts the fake TMDB sample films into an EMPTY catalog so the reference slice has data (<c>/films/550</c>)
/// without any API key. A real catalog comes from <c>--sync-catalog</c>. A missing database or table is logged, not fatal.
/// </summary>
public static partial class DevDataSeeder
{
    public static async Task RunAsync(IServiceProvider services, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(services);
        await using var scope = services.CreateAsyncScope();
        var time = scope.ServiceProvider.GetRequiredService<TimeProvider>();
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger(typeof(DevDataSeeder));

        try
        {
            // Resolved inside the try: without a connection string, creating the context throws InvalidOperationException.
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            if (await db.Films.AnyAsync(ct))
            {
                return;
            }

            foreach (var sample in FakeTmdbClient.Films)
            {
                var film = Film.Create(sample.TmdbId, sample.Title, time.GetUtcNow());
                film.UpdateDetails(sample.Title, sample.ReleaseYear, sample.Overview, sample.PosterPath, sample.RuntimeMinutes, [.. sample.Genres], sample.Popularity, time.GetUtcNow());
                db.Films.Add(film);
            }

            await db.SaveChangesAsync(ct);
            LogSeeded(logger);
        }
        catch (Exception e) when (e is NpgsqlException or InvalidOperationException)
        {
            LogSkipped(logger, e.Message);
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Seeded development films")]
    private static partial void LogSeeded(ILogger logger);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Dev seed skipped (is the database migrated?): {Reason}")]
    private static partial void LogSkipped(ILogger logger, string reason);
}
