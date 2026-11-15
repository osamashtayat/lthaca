using System.Security.Claims;
using Ithaca.Application.Auth;
using Ithaca.Application.Common;
using Ithaca.Application.Users;
using Ithaca.Domain.Common;
using Microsoft.AspNetCore.Authentication;
using Ithaca.Infrastructure.Persistence;
using Ithaca.Infrastructure.Persistence.Configurations;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Ithaca.Infrastructure.Identity;

/// <summary>
/// <see cref="ISessionManager"/> on ASP.NET Core Identity. Sessions are persistent cookies (lifetime: AuthSetup). Identity's
/// store and the services share the one scoped <see cref="AppDbContext"/>, which is what lets a deletion be staged.
/// </summary>
internal sealed class IdentitySessionManager(
    UserManager<ApplicationUser> users, SignInManager<ApplicationUser> signIn, AppDbContext db, TimeProvider time) : ISessionManager
{
    public async Task<Result<Guid>> RegisterAsync(string email, string password, string displayName, CancellationToken ct)
    {
        var now = time.GetUtcNow();
        var id = SequentialGuid.NewV7(now);
        var user = new ApplicationUser
        {
            Id = id,
            UserName = id.ToString("N"),
            Email = email,
            DisplayName = displayName,
            CreatedAt = now,
        };

        IdentityResult created;
        try
        {
            created = await users.CreateAsync(user, password);
        }
        catch (DbUpdateException e) when (e.InnerException is PostgresException
        {
            SqlState: PostgresErrorCodes.UniqueViolation, ConstraintName: ApplicationUserConfiguration.NormalizedEmailIndex,
        })
        {
            // Two sign-ups with the same email at once: Identity's uniqueness check passed for both, the index caught one.
            return AuthErrors.EmailTaken;
        }

        if (!created.Succeeded)
        {
            return ToError(created);
        }

        await signIn.SignInAsync(user, isPersistent: true);
        await UseUserPrincipalAsync(user);
        return id;
    }

    public async Task<Result<Guid>> PasswordSignInAsync(string email, string password, CancellationToken ct)
    {
        var user = await users.FindByEmailAsync(email);
        if (user is null)
        {
            HashAnyway(password);
            return AuthErrors.InvalidCredentials;
        }

        // Wrong password, locked out (incl. the attempt that locks it) and success-while-locked all look the same.
        var result = await signIn.PasswordSignInAsync(user, password, isPersistent: true, lockoutOnFailure: true);
        if (!result.Succeeded)
        {
            if (result.IsLockedOut)
            {
                HashAnyway(password); // Identity checks lockout before the password; keep the timing of a real check.
            }

            return AuthErrors.InvalidCredentials;
        }

        await UseUserPrincipalAsync(user);
        return user.Id;
    }

    public async Task SignInGuestAsync(Guid guestId, Guid roomId, string roomCode)
    {
        var identity = new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, guestId.ToString()),
                new Claim(ActorClaims.Kind, ActorClaims.GuestKind),
                new Claim(ActorClaims.RoomId, roomId.ToString()),
                new Claim(ActorClaims.RoomCode, roomCode),
            ],
            IdentityConstants.ApplicationScheme);
        var principal = new ClaimsPrincipal(identity);

        await signIn.Context.SignInAsync(IdentityConstants.ApplicationScheme, principal, new AuthenticationProperties { IsPersistent = true });
        signIn.Context.User = principal;
    }

    public async Task SignOutAsync()
    {
        await signIn.SignOutAsync();
        signIn.Context.User = new ClaimsPrincipal(new ClaimsIdentity());
    }

    public async Task<bool> SetDisplayNameAsync(Guid userId, string displayName, CancellationToken ct)
    {
        var user = await users.FindByIdAsync(userId.ToString());
        if (user is null)
        {
            return false;
        }

        user.DisplayName = displayName;
        return (await users.UpdateAsync(user)).Succeeded;
    }

    public async Task<bool> CheckPasswordAsync(Guid userId, string password, CancellationToken ct)
    {
        var user = await users.FindByIdAsync(userId.ToString());
        if (user is null)
        {
            HashAnyway(password);
            return false;
        }

        // Same lockout as sign-in (ADR-0023): a failure counts, and while locked out the answer is the same false.
        var result = await signIn.CheckPasswordSignInAsync(user, password, lockoutOnFailure: true);
        if (result.IsLockedOut)
        {
            HashAnyway(password);
        }

        return result.Succeeded;
    }

    public async Task<Result> ChangePasswordAsync(Guid userId, string currentPassword, string newPassword, CancellationToken ct)
    {
        var user = await users.FindByIdAsync(userId.ToString());
        if (user is null)
        {
            return AuthErrors.Unauthenticated;
        }

        // One update: the new hash and a new security stamp, which ends every other session (stamp checked per request, AuthSetup).
        var changed = await users.ChangePasswordAsync(user, currentPassword, newPassword);
        if (!changed.Succeeded)
        {
            return changed.Errors.Any(e => e.Code is nameof(IdentityErrorDescriber.PasswordMismatch))
                ? ProfileErrors.WrongPassword
                : Error.Validation("newPassword", changed.Errors.FirstOrDefault()?.Description ?? "That password can't be used.");
        }

        // This session carries the old stamp: issue a fresh cookie so the caller stays signed in.
        await signIn.SignInAsync(user, isPersistent: true);
        await UseUserPrincipalAsync(user);
        return Result.Success();
    }

    public async Task<bool> StageAccountDeletionAsync(Guid userId, CancellationToken ct)
    {
        var user = await db.Users.SingleOrDefaultAsync(u => u.Id == userId, ct);
        if (user is null)
        {
            return false;
        }

        db.Users.Remove(user);
        return true;
    }

    /// <summary>The cookie is written for the response; this request's principal switches too (antiforgery binds to it).</summary>
    private async Task UseUserPrincipalAsync(ApplicationUser user) =>
        signIn.Context.User = await signIn.CreateUserPrincipalAsync(user);

    /// <summary>
    /// Spends the same work as a password check when there is no hash to check against, so response time doesn't tell
    /// an unknown or locked account from a wrong password (ADR-0023).
    /// </summary>
    private void HashAnyway(string password)
    {
        var dummy = new ApplicationUser();
        _ = users.PasswordHasher.VerifyHashedPassword(dummy, _dummyHash ??= users.PasswordHasher.HashPassword(dummy, "not a real password"), password);
    }

    private static string? _dummyHash;

    private static Error ToError(IdentityResult result)
    {
        var errors = result.Errors.ToList();
        if (errors.Exists(e => e.Code is nameof(IdentityErrorDescriber.DuplicateEmail)))
        {
            return AuthErrors.EmailTaken;
        }

        var password = errors.Find(e => e.Code.StartsWith("Password", StringComparison.Ordinal));
        return password is not null
            ? Error.Validation("password", password.Description)
            : Error.Validation("email", errors.FirstOrDefault()?.Description ?? "That email can't be used.");
    }
}
