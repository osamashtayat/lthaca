using Ithaca.Domain.Moods;

namespace Ithaca.Application.Moods;

/// <summary>The one <see cref="MoodQuery"/> → <see cref="MoodQueryDto"/> mapping (solo decks now, room moods in BE-11).</summary>
public static class MoodQueryDtos
{
    public static MoodQueryDto From(MoodQuery mood)
    {
        ArgumentNullException.ThrowIfNull(mood);
        return new MoodQueryDto(
            mood.Id, mood.Moods, mood.MaxRuntime, mood.Language, mood.Text,
            new MoodFiltersDto(mood.IncludeGenres, mood.ExcludeGenres, mood.MaxRuntimeMinutes, mood.MinReleaseYear, mood.MaxReleaseYear, mood.Keywords),
            mood.TextIgnored);
    }
}
