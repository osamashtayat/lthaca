using Ithaca.Api;
using Ithaca.Api.Errors;
using Ithaca.Api.Hubs;
using Ithaca.Api.OpenApi;
using Ithaca.Application;
using Ithaca.Application.Realtime;
using Ithaca.Infrastructure;
using Ithaca.Infrastructure.Jobs;
using Ithaca.Infrastructure.Seeding;
using Microsoft.AspNetCore.Mvc.ModelBinding.Metadata;

// `--sync-catalog`: run one TMDB catalog sync and exit (a switch, not configuration; removed before config reads args).
const string SyncCatalogFlag = "--sync-catalog";
var syncCatalog = args.Contains(SyncCatalogFlag, StringComparer.Ordinal);
var builder = WebApplication.CreateBuilder(args.Where(a => a != SyncCatalogFlag).ToArray());

// Structured logs: the built-in JSON console formatter (no Serilog).
builder.Logging.ClearProviders();
builder.Logging.AddJsonConsole(options =>
{
    options.IncludeScopes = true;
    options.UseUtcTimestamp = true;
    options.TimestampFormat = "yyyy-MM-ddTHH:mm:ss.fffZ";
});

// Validation errors use JSON (camelCase) field paths; query-bound records bind camelCase names.
builder.Services
    .AddControllers(options =>
    {
        options.ModelMetadataDetailsProviders.Add(new SystemTextJsonValidationMetadataProvider());
        options.ModelMetadataDetailsProviders.Add(new CamelCaseBindingMetadataProvider());
    })
    .AddJsonOptions(options => JsonSetup.Configure(options.JsonSerializerOptions));
builder.Services.ConfigureHttpJsonOptions(options => JsonSetup.Configure(options.SerializerOptions));
builder.Services.AddIthacaProblemDetails();
builder.Services.AddIthacaOpenApi();

// Layer registrations: one extension method per layer (docs/backend-conventions.md).
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

// Real-time: the hub and its publisher live in the Api (ADR-0007).
builder.Services.AddSignalR(options => options.EnableDetailedErrors = false)
    .AddJsonProtocol(options => JsonSetup.Configure(options.PayloadSerializerOptions));
builder.Services.AddSingleton<IRoomEventPublisher, SignalRRoomEventPublisher>();

var app = builder.Build();

if (syncCatalog)
{
    Environment.ExitCode = await CatalogSyncCommand.RunAsync(app.Services, CancellationToken.None);
    return;
}

if (app.Environment.IsDevelopment())
{
    await DevDataSeeder.RunAsync(app.Services, CancellationToken.None);
    app.MapOpenApi();
}

app.UseExceptionHandler();
app.UseStatusCodePages();

app.MapControllers();
app.MapHub<RoomHub>(RoomEvents.HubPath);

await app.RunAsync();

/// <summary>Visible to WebApplicationFactory in Ithaca.Tests.</summary>
public partial class Program;
