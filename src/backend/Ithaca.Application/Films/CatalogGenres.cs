using Ithaca.Application.Common;
using Microsoft.EntityFrameworkCore;

namespace Ithaca.Application.Films;

/// <summary>The genres present in our catalog (available films), alphabetical: the one definition for browse and mood parsing.</summary>
public static class CatalogGenres
{
    public static async Task<List<string>> ListAsync(IAppDbContext db, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(db);
        return await db.Films
            .AsNoTracking()
            .Where(f => f.IsAvailable)
            .SelectMany(f => f.Genres)
            .Distinct()
            .OrderBy(g => g)
            .ToListAsync(ct);
    }
}
