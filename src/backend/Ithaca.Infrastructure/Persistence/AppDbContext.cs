using Ithaca.Application.Common;
using Ithaca.Domain.Films;
using Microsoft.EntityFrameworkCore;

namespace Ithaca.Infrastructure.Persistence;

/// <summary>The single EF Core context. Entity sets and configurations are added per feature.</summary>
public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options), IAppDbContext
{
    /// <summary>The pgvector extension; created by the initial migration (ADR-0002).</summary>
    public const string VectorExtension = "vector";

    public DbSet<Film> Films => Set<Film>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasPostgresExtension(VectorExtension);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
}
