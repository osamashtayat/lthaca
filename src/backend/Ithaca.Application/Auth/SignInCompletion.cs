using Ithaca.Application.Common;
using Ithaca.Application.Rooms;
using Microsoft.EntityFrameworkCore;

namespace Ithaca.Application.Auth;

/// <summary>
/// What every way of signing a user in ends with (register, sign-in, password reset): a guest caller's room membership
/// moves to the user (<see cref="GuestUpgrade"/>, ADR-0012), then the user is read back as the caller. A collaborator of
/// <c>AuthService</c> and <c>PasswordResetService</c>; it saves only when there was a guest membership to move.
/// </summary>
public sealed class SignInCompletion(IAppDbContext db, GuestUpgrade upgrade)
{
    /// <param name="previous">The caller as it was before the session switched to the user.</param>
    public async Task<Result<CurrentUserDto>> CompleteAsync(Actor? previous, Guid userId, CancellationToken ct)
    {
        if (await upgrade.StageAsync(previous, userId, ct))
        {
            await db.SaveChangesAsync(ct);
        }

        return await LoadUserAsync(userId, ct);
    }

    public async Task<Result<CurrentUserDto>> LoadUserAsync(Guid userId, CancellationToken ct)
    {
        var user = await db.Users
            .Where(u => u.Id == userId)
            .Select(u => new CurrentUserDto(PrincipalKind.User, u.Id, u.DisplayName, u.Email, null))
            .SingleOrDefaultAsync(ct);

        // A session that outlived its account counts as signed out.
        return user is null ? AuthErrors.Unauthenticated : user;
    }
}
