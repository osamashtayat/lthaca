using Ithaca.Application.Common;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Ithaca.Application.Ai;

/// <summary>
/// The daily AI spend cap (BE-19, L3) and the one way Application code calls the LLM. Each UTC day may spend
/// <see cref="AiOptions.MonthlyBudgetUsd"/> ÷ the days in that month, priced from each reply's token usage
/// (<see cref="AiOptions.Prices"/>). Once the day's share is spent, <see cref="CompleteAsync"/> answers
/// <see cref="AiErrors.BudgetReached"/> without calling the provider, and every caller falls back as it does on any AI failure:
/// mood text → chips only or <c>mood.text_unavailable</c>, picks → no reasons (docs/ai.md → Spend cap).
/// <para>A singleton holding the count in memory: one API instance today (OPS-06), and a restart starts the day's count again.
/// Each provider's own dashboard limit (OPS-10) is the hard backstop. A call that is already running when the share runs out
/// still finishes, so a day can go over by at most the calls in flight.</para>
/// </summary>
public sealed partial class AiBudget(ILlmClient llm, IOptions<AiOptions> options, TimeProvider time, ILogger<AiBudget> logger)
{
    private readonly Lock _gate = new();
    private DateOnly _day;
    private decimal _spentUsd;
    private bool _reachedLogged;

    /// <summary>
    /// The LLM call, unless today's share of the budget is spent; no answer within <paramref name="timeout"/> is
    /// <see cref="AiErrors.Unavailable"/> (someone is waiting on the page).
    /// </summary>
    public async Task<Result<LlmResponse>> CompleteAsync(LlmRequest request, TimeSpan timeout, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (IsReached())
        {
            return AiErrors.BudgetReached;
        }

        using var timer = new CancellationTokenSource(timeout, time);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, timer.Token);
        Result<LlmResponse> reply;
        try
        {
            reply = await llm.CompleteAsync(request, linked.Token);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            return AiErrors.TimedOut;
        }

        if (reply.IsSuccess && reply.Value.Usage is { } usage)
        {
            Spend(Cost(reply.Value.Model, usage));
        }

        return reply;
    }

    /// <summary>Whether today's (UTC) share is spent. Internal: callers just call <see cref="CompleteAsync"/>.</summary>
    internal bool IsReached()
    {
        lock (_gate)
        {
            var limit = RollOver();
            if (_spentUsd < limit)
            {
                return false;
            }

            if (!_reachedLogged)
            {
                _reachedLogged = true;
                LogReached(logger, limit);
            }

            return true;
        }
    }

    /// <summary>Today's share of the monthly budget.</summary>
    internal decimal DailyLimitUsd()
    {
        lock (_gate)
        {
            return RollOver();
        }
    }

    private void Spend(decimal usd)
    {
        lock (_gate)
        {
            RollOver();
            _spentUsd += usd;
        }
    }

    /// <summary>Starts a new count on a new UTC day; returns today's limit. Call under the lock.</summary>
    private decimal RollOver()
    {
        var now = time.GetUtcNow().UtcDateTime;
        var today = DateOnly.FromDateTime(now);
        if (today != _day)
        {
            _day = today;
            _spentUsd = 0;
            _reachedLogged = false;
        }

        return options.Value.MonthlyBudgetUsd / DateTime.DaysInMonth(now.Year, now.Month);
    }

    private decimal Cost(string model, LlmUsage usage)
    {
        var prices = options.Value.Prices;
        var price = prices.TryGetValue(model, out var known)
            ? known
            : new ModelPrice
            {
                InputPerMillionTokens = prices.Values.Select(p => p.InputPerMillionTokens).DefaultIfEmpty().Max(),
                OutputPerMillionTokens = prices.Values.Select(p => p.OutputPerMillionTokens).DefaultIfEmpty().Max(),
            };
        return ((usage.InputTokens * price.InputPerMillionTokens) + (usage.OutputTokens * price.OutputPerMillionTokens)) / 1_000_000m;
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Daily AI budget of {LimitUsd} USD reached; LLM calls are off until the next UTC day")]
    private static partial void LogReached(ILogger logger, decimal limitUsd);
}
