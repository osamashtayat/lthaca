using Ithaca.Application.Common;
using Ithaca.Domain.Errors;

namespace Ithaca.Application.Moods;

/// <summary>The mood errors, defined once. Messages never repeat the person's text (ADR-0022).</summary>
public static class MoodErrors
{
    public static readonly Error Unclear = new(ErrorCodes.MoodUnclear, "We couldn't find a mood in that text.");
}
