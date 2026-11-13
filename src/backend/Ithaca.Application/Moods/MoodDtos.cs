using System.ComponentModel.DataAnnotations;
using Ithaca.Domain.Moods;

namespace Ithaca.Application.Moods;

/// <summary>
/// What the person picked on the mood screen (part of <c>CreateDeckRequest</c>). Only <see cref="Text"/> goes to the LLM
/// (mood-parse.v1); moods, runtime and language are plain filters (docs/ai.md).
/// </summary>
public sealed record MoodInput(
    [MaxLength(MoodInput.MaxMoods)] IReadOnlyList<Mood> Moods,
    RuntimeLimit MaxRuntime,
    [StringLength(2, MinimumLength = 2)] string? Language,
    [MaxLength(MoodInput.TextMaxLength)] string? Text)
{
    public const int MaxMoods = 2;
    public const int TextMaxLength = 280;
}

/// <summary>The filters we derived from a <c>MoodQuery</c>: mood-parse.v1's validated output after our own checks.</summary>
public sealed record MoodFiltersDto(
    IReadOnlyList<string> IncludeGenres,
    IReadOnlyList<string> ExcludeGenres,
    int? MaxRuntimeMinutes,
    int? MinReleaseYear,
    int? MaxReleaseYear,
    IReadOnlyList<string> Keywords);

/// <summary>
/// A stored <c>MoodQuery</c>: the input as given plus the filters derived from it. <see cref="TextIgnored"/> is true when the
/// text wasn't a mood, so only the chips were used (ADR-0022).
/// </summary>
public sealed record MoodQueryDto(
    Guid Id,
    IReadOnlyList<Mood> Moods,
    RuntimeLimit MaxRuntime,
    string? Language,
    string? Text,
    MoodFiltersDto Filters,
    bool TextIgnored);
