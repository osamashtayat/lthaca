using System.Buffers.Binary;
using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;
using Ithaca.Application.Auth;
using Microsoft.AspNetCore.DataProtection;

namespace Ithaca.Infrastructure.Identity;

/// <summary>
/// Password reset tokens (api-contract.md §4): the user id, the issue time and the account's security stamp, encrypted and
/// signed with ASP.NET Data Protection, base64url-encoded so the token alone identifies the account. A token is valid for
/// <see cref="PasswordResetTicket.Lifetime"/> by <see cref="TimeProvider"/> and only while the stamp is unchanged: any
/// password change (a reset included) changes it, which makes every outstanding token, the used one too, invalid. Our own
/// format rather than Identity's <c>DataProtectorTokenProvider</c>, whose token needs the user up front and whose clock
/// can't be faked.
/// </summary>
internal sealed class PasswordResetTokens(IDataProtectionProvider protection, TimeProvider time)
{
    private const byte Version = 1;
    private const int HeaderLength = 1 + 16 + 8; // version, user id, issued-at ticks (UTC)

    private readonly IDataProtector _protector = protection.CreateProtector("Ithaca.PasswordReset.v1");

    public string Create(Guid userId, string securityStamp)
    {
        var stamp = Encoding.UTF8.GetBytes(securityStamp);
        var payload = new byte[HeaderLength + stamp.Length];
        payload[0] = Version;
        userId.TryWriteBytes(payload.AsSpan(1, 16));
        BinaryPrimitives.WriteInt64BigEndian(payload.AsSpan(17, 8), time.GetUtcNow().UtcTicks);
        stamp.CopyTo(payload, HeaderLength);
        return Base64Url.EncodeToString(_protector.Protect(payload));
    }

    /// <summary>The user and stamp a token carries when it is authentic and unexpired; null otherwise.</summary>
    public (Guid UserId, string SecurityStamp)? Read(string token)
    {
        byte[] payload;
        try
        {
            payload = _protector.Unprotect(Base64Url.DecodeFromChars(token));
        }
        catch (Exception e) when (e is FormatException or CryptographicException)
        {
            return null;
        }

        if (payload.Length <= HeaderLength || payload[0] != Version)
        {
            return null;
        }

        var issued = new DateTimeOffset(BinaryPrimitives.ReadInt64BigEndian(payload.AsSpan(17, 8)), TimeSpan.Zero);
        var age = time.GetUtcNow() - issued;
        if (age < TimeSpan.Zero || age > PasswordResetTicket.Lifetime)
        {
            return null;
        }

        return (new Guid(payload.AsSpan(1, 16)), Encoding.UTF8.GetString(payload.AsSpan(HeaderLength)));
    }
}
