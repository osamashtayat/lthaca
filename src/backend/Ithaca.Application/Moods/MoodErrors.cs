using Ithaca.Application.Common;
using Ithaca.Domain.Errors;

namespace Ithaca.Application.Moods;

/// <summary>The mood errors, defined once. Messages never repeat the person's text (ADR-0022).</summary>
public static class MoodErrors
{
    public static readonly Error Unclear = new(ErrorCodes.MoodUnclear, "We couldn't find a mood in that text.");

    /// <summary>The text couldn't be read right now (AI failure or budget) and there are no chips to fall back on (BE-19).</summary>
    public static readonly Error TextUnavailable = new(ErrorCodes.MoodTextUnavailable, "Mood text can't be read right now; pick a few moods instead.");
}
