using Ithaca.Application.Auth;
using Ithaca.Application.Common;
using Ithaca.Application.Realtime;
using Ithaca.Application.Rooms;
using Ithaca.Domain.Rooms;
using Microsoft.EntityFrameworkCore;

namespace Ithaca.Application.Users;

/// <summary>
/// The signed-in user's profile and account (api-contract.md §14): display name, password change, account deletion. Users
/// only (ADR-0023). The account itself is Identity's, reached through <see cref="ISessionManager"/>. Password change and
/// deletion re-authenticate with the current password, which counts toward the sign-in lockout.
/// </summary>
public sealed class ProfileService(
    IAppDbContext db, ICurrentActor current, ISessionManager sessions, IRoomEventPublisher events, TimeProvider time)
{
    /// <summary>Tries at deleting when a concurrent start changed a room the user hosts (rooms carry a concurrency token).</summary>
    private const int WriteAttempts = 3;

    public async Task<Result<ProfileDto>> GetAsync(CancellationToken ct)
    {
        if (UserId() is not { } userId)
        {
            return AuthErrors.Unauthenticated;
        }

        var profile = await db.Users
            .Where(u => u.Id == userId)
            .Select(u => new ProfileDto(u.Id, u.DisplayName, u.Email, u.CreatedAt))
            .SingleOrDefaultAsync(ct);

        // A session that outlived its account counts as signed out.
        return profile is null ? AuthErrors.Unauthenticated : profile;
    }

    /// <summary>Sets the display name (trimmed); room member names already shown keep theirs.</summary>
    public async Task<Result<ProfileDto>> UpdateAsync(UpdateProfileRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (UserId() is not { } userId)
        {
            return AuthErrors.Unauthenticated;
        }

        var displayName = request.DisplayName.Trim();
        if (displayName.Length == 0)
        {
            return Error.Validation("displayName", "Enter a display name.");
        }

        return await sessions.SetDisplayNameAsync(userId, displayName, ct) ? await GetAsync(ct) : AuthErrors.Unauthenticated;
    }

    /// <summary>Checks the current password, sets the new one, keeps this session and ends the user's other sessions.</summary>
    public async Task<Result> ChangePasswordAsync(ChangePasswordRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (UserId() is not { } userId)
        {
            return AuthErrors.Unauthenticated;
        }

        if (!await sessions.CheckPasswordAsync(userId, request.CurrentPassword, ct))
        {
            return ProfileErrors.WrongPassword;
        }

        return await sessions.ChangePasswordAsync(userId, request.CurrentPassword, request.NewPassword, ct);
    }

    /// <summary>
    /// Checks the password, closes the open rooms the user hosts, deletes the account and everything that belongs to it
    /// (FK cascade: library, decks, moods, imports, room memberships and their votes) in one commit, signs the caller out,
    /// then announces <c>roomClosed</c> / <c>memberLeft</c> in the open rooms they were in.
    /// </summary>
    public async Task<Result> DeleteAccountAsync(DeleteAccountRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (UserId() is not { } userId)
        {
            return AuthErrors.Unauthenticated;
        }

        // First: a failed check saves the failure count, which must not carry anything staged with it.
        if (!await sessions.CheckPasswordAsync(userId, request.Password, ct))
        {
            return ProfileErrors.WrongPassword;
        }

        var memberships = await db.RoomMembers
            .AsNoTracking()
            .Where(m => m.UserId == userId && m.LeftAt == null)
            .Join(
                db.Rooms.Where(r => r.Status == RoomStatus.Lobby || r.Status == RoomStatus.Swiping),
                m => m.RoomId,
                r => r.Id,
                (m, r) => new { m.RoomId, MemberId = m.Id })
            .ToListAsync(ct);
        var hosted = new List<Guid>();
        var left = new List<RoomMemberEvent>();
        foreach (var membership in memberships)
        {
            var hostId = await RoomAccess.InHostOrder(db.RoomMembers.AsNoTracking().Where(m => m.RoomId == membership.RoomId && m.LeftAt == null))
                .Select(m => m.Id)
                .FirstAsync(ct);
            if (hostId == membership.MemberId)
            {
                hosted.Add(membership.RoomId);
            }
            else
            {
                left.Add(new RoomMemberEvent(membership.RoomId, membership.MemberId));
            }
        }

        if (!await sessions.StageAccountDeletionAsync(userId, ct))
        {
            return AuthErrors.Unauthenticated;
        }

        var now = time.GetUtcNow();
        var closed = new List<Guid>();
        for (var attempt = 1; ; attempt++)
        {
            closed.Clear();
            foreach (var roomId in hosted)
            {
                if ((await db.Rooms.SingleAsync(r => r.Id == roomId, ct)).Close(now))
                {
                    closed.Add(roomId);
                }
            }

            try
            {
                await db.SaveChangesAsync(ct);
                break;
            }
            catch (Exception e) when (WriteConflicts.IsWriteConflict(e) && closed.Count > 0 && attempt < WriteAttempts)
            {
                // A start changed one of these rooms at the same moment: read them again and close again.
                foreach (var roomId in closed)
                {
                    await db.Rooms.Entry(await db.Rooms.SingleAsync(r => r.Id == roomId, ct)).ReloadAsync(ct);
                }
            }
        }

        await sessions.SignOutAsync();
        foreach (var roomId in closed)
        {
            await events.PublishAsync(roomId, RoomEvents.RoomClosed, new RoomEvent(roomId), ct);
        }

        foreach (var member in left)
        {
            await events.PublishAsync(member.RoomId, RoomEvents.MemberLeft, member, ct);
        }

        return Result.Success();
    }

    private Guid? UserId() => current.Actor is { IsUser: true } actor ? actor.Id : null;
}
