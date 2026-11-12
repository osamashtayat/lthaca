using Ithaca.Application.Catalog;
using Microsoft.Extensions.DependencyInjection;

namespace Ithaca.Infrastructure.Jobs;

/// <summary>
/// <c>dotnet run --project src/backend/Ithaca.Api -- --sync-catalog</c>: one catalog sync, then exit. Same service as the
/// nightly job; the command only opens a scope and prints the outcome.
/// </summary>
public static class CatalogSyncCommand
{
    public static async Task<int> RunAsync(IServiceProvider services, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(services);
        await using var scope = services.CreateAsyncScope();
        var result = await scope.ServiceProvider.GetRequiredService<CatalogSyncService>().RunAsync(ct);
        Console.WriteLine(result.IsSuccess ? $"Catalog sync finished: {result.Value}" : $"Catalog sync failed: {result.Error.Code} ({result.Error.Message})");
        return result.IsSuccess ? 0 : 1;
    }
}
