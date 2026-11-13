using Microsoft.EntityFrameworkCore;

namespace Ithaca.Application.Common;

/// <summary>
/// Recognises a <c>SaveChangesAsync</c> that failed because a concurrent request wrote first (unique key, deadlock).
/// Npgsql's retrying execution strategy wraps a transient one (e.g. a deadlock) in an <see cref="InvalidOperationException"/>
/// instead of throwing <see cref="DbUpdateException"/>; both mean "re-check and decide", never "swallow".
/// </summary>
public static class WriteConflicts
{
    public static bool IsWriteConflict(Exception exception) =>
        exception is DbUpdateException || exception is InvalidOperationException { InnerException: DbUpdateException };
}
