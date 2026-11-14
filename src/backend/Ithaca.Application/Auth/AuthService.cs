using Ithaca.Application.Common;
using Ithaca.Application.Rooms;
using Microsoft.EntityFrameworkCore;

namespace Ithaca.Application.Auth;

/// <summary>
/// Sessions for users and guests (ADR-0012, api-contract.md §4). The caller always comes from <see cref="ICurrentActor"/>;
/// the cookie itself is <see cref="ISessionManager"/>'s. Registering or signing in while holding a guest cookie is a guest
/// upgrade (<see cref="GuestUpgrade"/>): the guest's room membership moves to the user.
/// </summary>
public sealed class AuthService(ISessionManager sessions, ICurrentActor current, IAppDbContext db, GuestUpgrade upgrade)
{
    public Task<Result<CurrentUserDto>> GetCurrentAsync(CancellationToken ct) =>
        current.Actor is { } actor ? LoadAsync(actor, ct) : Task.FromResult<Result<CurrentUserDto>>(AuthErrors.Unauthenticated);

    /// <summary>Creates the account and signs it in; a guest caller's room membership moves to it.</summary>
    public async Task<Result<CurrentUserDto>> RegisterAsync(RegisterRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        var displayName = request.DisplayName.Trim();
        if (displayName.Length == 0)
        {
            return Error.Validation("displayName", "Enter a display name.");
        }

        var guest = current.Actor; // read before the session switches to the new user
        var signedIn = await sessions.RegisterAsync(request.Email.Trim(), request.Password, displayName, ct);
        return signedIn.IsSuccess ? await UpgradeAndLoadAsync(guest, signedIn.Value, ct) : signedIn.Error;
    }

    public async Task<Result<CurrentUserDto>> SignInAsync(SignInRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        var guest = current.Actor; // read before the session switches to the user
        var signedIn = await sessions.PasswordSignInAsync(request.Email.Trim(), request.Password, ct);
        return signedIn.IsSuccess ? await UpgradeAndLoadAsync(guest, signedIn.Value, ct) : signedIn.Error;
    }

    /// <summary>Clears the cookie of whoever is signed in; signing out twice is not an error.</summary>
    public async Task<Result> SignOutAsync(CancellationToken ct)
    {
        await sessions.SignOutAsync();
        return Result.Success();
    }

    private async Task<Result<CurrentUserDto>> UpgradeAndLoadAsync(Actor? previous, Guid userId, CancellationToken ct)
    {
        if (await upgrade.StageAsync(previous, userId, ct))
        {
            await db.SaveChangesAsync(ct);
        }

        return await LoadUserAsync(userId, ct);
    }

    private Task<Result<CurrentUserDto>> LoadAsync(Actor actor, CancellationToken ct) =>
        actor.IsUser ? LoadUserAsync(actor.Id, ct) : LoadGuestAsync(actor, ct);

    private async Task<Result<CurrentUserDto>> LoadUserAsync(Guid userId, CancellationToken ct)
    {
        var user = await db.Users
            .Where(u => u.Id == userId)
            .Select(u => new CurrentUserDto(PrincipalKind.User, u.Id, u.DisplayName, u.Email, null))
            .SingleOrDefaultAsync(ct);

        // A session that outlived its account counts as signed out.
        return user is null ? AuthErrors.Unauthenticated : user;
    }

    private async Task<Result<CurrentUserDto>> LoadGuestAsync(Actor actor, CancellationToken ct)
    {
        var guest = await db.Guests
            .AsNoTracking()
            .Where(g => g.Id == actor.Id)
            .Select(g => new CurrentUserDto(PrincipalKind.Guest, g.Id, g.DisplayName, null, actor.RoomCode))
            .SingleOrDefaultAsync(ct);

        return guest is null ? AuthErrors.Unauthenticated : guest;
    }
}
