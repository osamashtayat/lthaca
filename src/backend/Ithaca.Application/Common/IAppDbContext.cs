using Ithaca.Domain.Films;
using Microsoft.EntityFrameworkCore;

namespace Ithaca.Application.Common;

/// <summary>
/// The Application layer's unit of work (ADR-0001). Only use-case services use it, and they call
/// <see cref="SaveChangesAsync"/> exactly once per use case. Entity sets are added as their features land;
/// there is deliberately no generic repository.
/// </summary>
public interface IAppDbContext
{
    DbSet<Film> Films { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken);
}
