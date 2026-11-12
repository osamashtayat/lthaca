using Ithaca.Domain.Common;
using Ithaca.Domain.Errors;

namespace Ithaca.Domain.Users;

/// <summary>
/// Someone who joined a room from a shared link without an account (glossary, ADR-0012). Their cookie is valid for that
/// one room only; the room membership itself is a <c>RoomMember</c> (BE-10).
/// </summary>
public sealed class Guest
{
    public const int DisplayNameMaxLength = 30;

    private Guest(string displayName, DateTimeOffset now)
    {
        Id = SequentialGuid.NewV7(now);
        DisplayName = displayName;
        CreatedAt = now;
    }

    /// <summary>For persistence materialization only.</summary>
    private Guest()
    {
        DisplayName = "";
    }

    public Guid Id { get; private set; }

    public string DisplayName { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public static Guest Create(string displayName, DateTimeOffset now)
    {
        var name = displayName?.Trim();
        return string.IsNullOrEmpty(name) || name.Length > DisplayNameMaxLength
            ? throw new DomainException(ErrorCodes.Validation, $"displayName must be 1-{DisplayNameMaxLength} characters.", "displayName")
            : new Guest(name, now);
    }
}
