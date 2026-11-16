using System.ComponentModel.DataAnnotations;
using Ithaca.Application.Common;

namespace Ithaca.Api.RateLimiting;

/// <summary>
/// <c>RateLimits</c> section (BE-19): one rule per policy in <see cref="RateLimitPolicies"/>. Auth and room join count by IP;
/// the others per person (user or guest), or by IP without a session. Limits live here, not in code. Rules are checked at
/// startup by <see cref="RateLimitSetup"/> (DataAnnotations don't reach nested objects).
/// </summary>
public sealed class RateLimitOptions : IConfigSection
{
    public static string SectionName => "RateLimits";

    /// <summary>Off only in tests that aren't about limits (many requests from one test client).</summary>
    public bool Enabled { get; init; } = true;

    /// <summary>Register, sign-in, forgot and reset password.</summary>
    [Required]
    public RateLimitRule Auth { get; init; } = new() { PermitLimit = 20, Window = TimeSpan.FromMinutes(15) };

    /// <summary><c>POST /api/rooms/{code}/join</c>.</summary>
    [Required]
    public RateLimitRule RoomJoin { get; init; } = new() { PermitLimit = 20, Window = TimeSpan.FromMinutes(10) };

    /// <summary><c>POST /api/decks</c> (any source; a mood deck's AI call included).</summary>
    [Required]
    public RateLimitRule DeckCreate { get; init; } = new() { PermitLimit = 30, Window = TimeSpan.FromHours(1) };

    /// <summary><c>POST /api/imports</c> (ADR-0024: 5 per user per day).</summary>
    [Required]
    public RateLimitRule ImportCreate { get; init; } = new() { PermitLimit = 5, Window = TimeSpan.FromDays(1) };

    /// <summary>The other AI-backed calls: room mood step, solo top 3, room result.</summary>
    [Required]
    public RateLimitRule Ai { get; init; } = new() { PermitLimit = 60, Window = TimeSpan.FromMinutes(10) };

    /// <summary>
    /// Proxies (CIDR) whose <c>X-Forwarded-For</c> we believe, so per-IP limits see the client and not nginx. Loopback is
    /// always trusted. Only the last hop is read.
    /// </summary>
    public List<string> TrustedProxyNetworks { get; init; } = [];

    public RateLimitRule RuleFor(string policy) => policy switch
    {
        RateLimitPolicies.Auth => Auth,
        RateLimitPolicies.RoomJoin => RoomJoin,
        RateLimitPolicies.DeckCreate => DeckCreate,
        RateLimitPolicies.ImportCreate => ImportCreate,
        RateLimitPolicies.Ai => Ai,
        _ => throw new ArgumentOutOfRangeException(nameof(policy), policy, "Unknown rate limit policy."),
    };
}

/// <summary>At most <see cref="PermitLimit"/> requests per <see cref="Window"/>, counted from a partition's first request.</summary>
public sealed class RateLimitRule
{
    /// <summary>1–100 000.</summary>
    public int PermitLimit { get; init; }

    /// <summary>1 second to 7 days.</summary>
    public TimeSpan Window { get; init; }
}

/// <summary>The policy names used in <c>[EnableRateLimiting]</c> on controller actions.</summary>
public static class RateLimitPolicies
{
    public const string Auth = "auth";
    public const string RoomJoin = "room-join";
    public const string DeckCreate = "deck-create";
    public const string ImportCreate = "import-create";
    public const string Ai = "ai";

    public static readonly IReadOnlyList<string> All = [Auth, RoomJoin, DeckCreate, ImportCreate, Ai];
}
