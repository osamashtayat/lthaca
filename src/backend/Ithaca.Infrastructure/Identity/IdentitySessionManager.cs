using System.Security.Claims;
using Ithaca.Application.Auth;
using Ithaca.Application.Common;
using Ithaca.Domain.Common;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;

namespace Ithaca.Infrastructure.Identity;

/// <summary><see cref="ISessionManager"/> on ASP.NET Core Identity. Sessions are persistent cookies (lifetime: AuthSetup).</summary>
internal sealed class IdentitySessionManager(
    UserManager<ApplicationUser> users, SignInManager<ApplicationUser> signIn, TimeProvider time) : ISessionManager
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

        var created = await users.CreateAsync(user, password);
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
            return AuthErrors.InvalidCredentials;
        }

        // Wrong password, locked out (incl. the attempt that locks it) and success-while-locked all look the same.
        var result = await signIn.PasswordSignInAsync(user, password, isPersistent: true, lockoutOnFailure: true);
        if (!result.Succeeded)
        {
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

    /// <summary>The cookie is written for the response; this request's principal switches too (antiforgery binds to it).</summary>
    private async Task UseUserPrincipalAsync(ApplicationUser user) =>
        signIn.Context.User = await signIn.CreateUserPrincipalAsync(user);

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
