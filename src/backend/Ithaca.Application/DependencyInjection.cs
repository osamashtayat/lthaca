using Ithaca.Application.Auth;
using Ithaca.Application.Catalog;
using Ithaca.Application.Decks;
using Ithaca.Application.Films;
using Ithaca.Application.Imports;
using Ithaca.Application.Library;
using Ithaca.Application.Moods;
using Ithaca.Application.Rooms;
using Ithaca.Application.Taste;
using Ithaca.Application.Users;
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
        services.AddScoped<WikidataEnricher>(); // collaborator of CatalogSyncService only, not a use-case service
        services.AddScoped<FilmEmbedder>(); // collaborator of CatalogSyncService only, not a use-case service
        services.AddScoped<FilmTrailerService>();
        services.AddScoped<AuthService>();
        services.AddScoped<MoodInterpreter>(); // collaborator of DeckService (and RoomService in BE-11), not a use-case service
        services.AddScoped<DeckService>();
        services.AddScoped<RoomAccess>(); // collaborator of RoomService and RoomMembershipService, not a use-case service
        services.AddScoped<GuestUpgrade>(); // collaborator of AuthService, not a use-case service
        services.AddScoped<RoomService>();
        services.AddScoped<RoomMembershipService>();
        services.AddScoped<LibraryService>();
        services.AddScoped<TasteService>();
        services.AddScoped<ProfileService>();
        services.AddScoped<ImportService>();
        return services;
    }
}
