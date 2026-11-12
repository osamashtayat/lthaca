using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Ithaca.Infrastructure.Persistence;

/// <summary>
/// Lets <c>dotnet ef</c> build the model without starting the Api. Reads the same <c>ConnectionStrings__Ithaca</c>
/// environment variable; generating a migration needs no live database.
/// </summary>
internal sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__" + PersistenceOptions.ConnectionStringName)
            ?? "Host=localhost;Database=ithaca_design_time";

        var options = new DbContextOptionsBuilder<AppDbContext>();
        PersistenceOptions.Configure(options, connectionString);
        return new AppDbContext(options.Options);
    }
}
