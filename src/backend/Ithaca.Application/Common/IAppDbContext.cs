using Ithaca.Application.Users;
using Ithaca.Domain.Films;
using Ithaca.Domain.Users;
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

    /// <summary>Registered users, read-only: accounts are created and changed through <c>ISessionManager</c> (Identity).</summary>
    IQueryable<UserReadModel> Users { get; }

    DbSet<Guest> Guests { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken);
}
