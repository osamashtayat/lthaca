using System.ComponentModel.DataAnnotations;
using Ithaca.Application.Common;

namespace Ithaca.Application.Taste;

/// <summary><c>Taste</c> section: when By taste is ready (api-contract.md §12, A16).</summary>
public sealed class TasteOptions : IConfigSection
{
    public static string SectionName => "Taste";

    /// <summary>Films with a positive signal (liked, rated ≥ 3.5, or "loved it") needed for By taste (A16: 10).</summary>
    [Range(1, 1_000)]
    public int SignalsNeeded { get; init; } = 10;
}
