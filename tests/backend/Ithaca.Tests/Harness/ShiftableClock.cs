namespace Ithaca.Tests.Harness;

/// <summary>The system clock moved forward by <see cref="Offset"/>; timers keep real time. For "an hour later" in a running host.</summary>
internal sealed class ShiftableClock : TimeProvider
{
    public TimeSpan Offset { get; set; }

    public override DateTimeOffset GetUtcNow() => System.GetUtcNow() + Offset;
}
