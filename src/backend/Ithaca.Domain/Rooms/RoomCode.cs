using System.Security.Cryptography;

namespace Ithaca.Domain.Rooms;

/// <summary>
/// A room's share code, e.g. <c>POP-428</c>: three letters, a dash, three digits (100–999). Case-insensitive; stored
/// upper case. Drawn from a cryptographic RNG because anyone holding the code can join.
/// </summary>
public static class RoomCode
{
    public const int Length = 7;

    private const string Letters = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";

    public static string New() =>
        string.Create(Length, 0, static (span, _) =>
        {
            for (var i = 0; i < 3; i++)
            {
                span[i] = Letters[RandomNumberGenerator.GetInt32(Letters.Length)];
            }

            span[3] = '-';
            RandomNumberGenerator.GetInt32(100, 1000).TryFormat(span[4..], out _, provider: System.Globalization.CultureInfo.InvariantCulture);
        });

    /// <summary>The stored form of what someone typed or linked (<c>pop-428 </c> → <c>POP-428</c>); null when it can't be a code.</summary>
    public static string? Normalize(string? code)
    {
        var value = code?.Trim().ToUpperInvariant();
        return value is { Length: Length } && IsLetters(value.AsSpan(0, 3)) && value[3] == '-' && IsDigits(value.AsSpan(4)) ? value : null;
    }

    private static bool IsLetters(ReadOnlySpan<char> span) => !span.ContainsAnyExceptInRange('A', 'Z');

    private static bool IsDigits(ReadOnlySpan<char> span) => !span.ContainsAnyExceptInRange('0', '9');
}
