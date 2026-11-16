using System.ComponentModel.DataAnnotations;
using Ithaca.Domain.Moods;

namespace Ithaca.Application.Moods;

/// <summary>
/// What the person picked on the mood screen (part of <c>CreateDeckRequest</c>). Only <see cref="Text"/> goes to the LLM
/// (mood-parse.v2); moods, runtime and language are plain filters (docs/ai.md). Limits live on <see cref="MoodQuery"/>.
/// </summary>
public sealed record MoodInput(
    [MaxLength(MoodQuery.MaxMoods)] IReadOnlyList<Mood> Moods,
    RuntimeLimit MaxRuntime,
    [StringLength(MoodQuery.LanguageLength, MinimumLength = MoodQuery.LanguageLength)] string? Language,
    [MaxLength(MoodQuery.TextMaxLength)] string? Text);

/// <summary>The filters we derived from a <c>MoodQuery</c>: mood-parse.v2's validated output after our own checks.</summary>
public sealed record MoodFiltersDto(
    IReadOnlyList<string> IncludeGenres,
    IReadOnlyList<string> ExcludeGenres,
    int? MaxRuntimeMinutes,
    int? MinReleaseYear,
    int? MaxReleaseYear,
    IReadOnlyList<string> Keywords);

/// <summary>
/// A stored <c>MoodQuery</c>: the input as given plus the filters derived from it. <see cref="TextIgnored"/> is true when the
/// text wasn't used, so only the chips were (ADR-0022); <see cref="TextUnavailable"/> is also true when that was because we
/// couldn't read the text right now (AI failure or the daily AI budget), not because it wasn't a mood (BE-19).
/// </summary>
public sealed record MoodQueryDto(
    Guid Id,
    IReadOnlyList<Mood> Moods,
    RuntimeLimit MaxRuntime,
    string? Language,
    string? Text,
    MoodFiltersDto Filters,
    bool TextIgnored,
    bool TextUnavailable);
