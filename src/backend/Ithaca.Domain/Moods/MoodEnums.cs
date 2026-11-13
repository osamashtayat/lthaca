namespace Ithaca.Domain.Moods;

/// <summary>A mood chip. The labels are UI copy in the frontend; the server owns what each one means (api-contract.md §3).</summary>
public enum Mood
{
    Cozy,
    Laugh,
    KeepGuessing,
    MakeMeFeel,
    TakeMeAway,
    Surprise,
}

/// <summary>How long a film may run: a plain filter, never LLM input.</summary>
public enum RuntimeLimit
{
    Under90,
    UpTo2h,
    Any,
}

/// <summary>A room member's private "never show me" filter (glossary: Hard no).</summary>
public enum HardNo
{
    Horror,
    Subtitles,
    Gore,
    Musicals,
    SadEndings,
}

/// <summary>The one meaning of <see cref="RuntimeLimit"/> in minutes, for browse and moods alike.</summary>
public static class RuntimeLimits
{
    /// <summary>The longest runtime that passes: under 90 → 89, up to 2h → 120, any → no limit.</summary>
    public static int? MaxMinutes(this RuntimeLimit limit) => limit switch
    {
        RuntimeLimit.Under90 => 89,
        RuntimeLimit.UpTo2h => 120,
        _ => null,
    };
}
