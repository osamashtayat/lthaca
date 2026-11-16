using System.Threading.RateLimiting;

namespace Ithaca.Api.RateLimiting;

/// <summary>
/// A fixed-window limiter on <see cref="TimeProvider"/> (the framework's own limiters read the system clock, so tests
/// couldn't move time). The window opens at the first request after the last one closed; requests over the limit are
/// refused at once (no queue) with <see cref="MetadataName.RetryAfter"/> set to the time left in the window.
/// </summary>
internal sealed class WindowRateLimiter(RateLimitRule rule, TimeProvider time) : RateLimiter
{
    private readonly Lock _gate = new();
    private DateTimeOffset _windowEnd = DateTimeOffset.MinValue;
    private int _used;

    public override TimeSpan? IdleDuration
    {
        get
        {
            lock (_gate)
            {
                var now = time.GetUtcNow();
                return now >= _windowEnd ? now - _windowEnd : null;
            }
        }
    }

    public override RateLimiterStatistics? GetStatistics()
    {
        lock (_gate)
        {
            return new RateLimiterStatistics { CurrentAvailablePermits = time.GetUtcNow() >= _windowEnd ? rule.PermitLimit : rule.PermitLimit - _used };
        }
    }

    protected override RateLimitLease AttemptAcquireCore(int permitCount)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(permitCount);
        lock (_gate)
        {
            var now = time.GetUtcNow();
            if (now >= _windowEnd)
            {
                _windowEnd = now + rule.Window;
                _used = 0;
            }

            if (_used + permitCount <= rule.PermitLimit)
            {
                _used += permitCount;
                return Lease.Acquired;
            }

            return new Lease(false, _windowEnd - now);
        }
    }

    protected override ValueTask<RateLimitLease> AcquireAsyncCore(int permitCount, CancellationToken cancellationToken) =>
        ValueTask.FromResult(AttemptAcquireCore(permitCount));

    private sealed class Lease(bool isAcquired, TimeSpan? retryAfter) : RateLimitLease
    {
        public static readonly Lease Acquired = new(true, null);

        public override bool IsAcquired => isAcquired;

        public override IEnumerable<string> MetadataNames => retryAfter is null ? [] : [MetadataName.RetryAfter.Name];

        public override bool TryGetMetadata(string metadataName, out object? metadata)
        {
            if (retryAfter is { } wait && metadataName == MetadataName.RetryAfter.Name)
            {
                metadata = wait;
                return true;
            }

            metadata = null;
            return false;
        }
    }
}
