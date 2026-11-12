using Ithaca.Application.Tmdb;
using Ithaca.Domain.Films;
using Ithaca.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace Ithaca.Infrastructure.Seeding;

/// <summary>
/// Development only: puts the fake TMDB films into an empty catalog so the reference slice has data
/// (<c>/films/550</c>). Never runs in Production. A missing database or table is logged, not fatal.
/// </summary>
public static partial class DevDataSeeder
{
    public static async Task RunAsync(IServiceProvider services, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(services);
        await using var scope = services.CreateAsyncScope();
        var tmdb = scope.ServiceProvider.GetRequiredService<ITmdbClient>();
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

            foreach (var summary in await tmdb.GetCatalogPageAsync(1, ct))
            {
                var details = await tmdb.GetFilmDetailsAsync(summary.TmdbId, ct);
                var film = Film.Create(summary.TmdbId, summary.Title, time.GetUtcNow());
                if (details is not null)
                {
                    film.UpdateDetails(details.ReleaseYear, details.Overview, details.PosterPath, details.RuntimeMinutes, [.. details.Genres], time.GetUtcNow());
                }

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
