using Ithaca.Application.Auth;
using Ithaca.Application.Catalog;
using Ithaca.Application.Decks;
using Ithaca.Application.Films;
using Ithaca.Application.Library;
using Ithaca.Application.Rooms;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Ithaca.Application;

public static class DependencyInjection
{
    /// <summary>
    /// Registers Application services. Use-case services are concrete sealed classes, registered directly here
    /// (one line per service). Interfaces exist only for boundaries Infrastructure implements.
    /// </summary>
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        // All time comes from TimeProvider so tests can substitute a fake clock.
        services.TryAddSingleton(TimeProvider.System);
        services.AddScoped<FilmQueryService>();
        services.AddScoped<CatalogSyncService>();
        services.AddScoped<WikidataEnrichmentService>();
        services.AddScoped<FilmTrailerService>();
        services.AddScoped<AuthService>();
        services.AddScoped<DeckService>();
        services.AddScoped<RoomService>();
        services.AddScoped<LibraryService>();
        return services;
    }
}
