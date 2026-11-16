namespace Ithaca.Domain.Decks;

/// <summary>
/// The one-line reason shown under a pick: a solo top-3 pick or a room's picked film (BE-13). Written by the AI from
/// Wikidata facts and our own data only (ADR-0017); a pick without one simply shows none.
/// </summary>
public static class PickReason
{
    /// <summary>Longer replies are dropped, not cut: a one-liner that runs on isn't one.</summary>
    public const int MaxLength = 200;

    /// <summary>The reason trimmed, or null when it is empty or longer than <see cref="MaxLength"/>.</summary>
    public static string? Clean(string? reason) =>
        reason?.Trim() is { Length: > 0 and <= MaxLength } trimmed ? trimmed : null;
}
