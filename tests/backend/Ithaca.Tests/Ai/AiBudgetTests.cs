using Ithaca.Application.Ai;
using Ithaca.Application.Common;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;

namespace Ithaca.Tests.Ai;

/// <summary>BE-19: the daily AI spend cap (<see cref="AiBudget"/>), priced from token usage, on a fake clock.</summary>
public sealed class AiBudgetTests
{
    private static readonly PromptId Prompt = new("mood-parse", 2);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    /// <summary>November: 30 days, so $30 a month is $1 a day.</summary>
    private static FakeTimeProvider November() => new(new DateTimeOffset(2026, 11, 16, 8, 0, 0, TimeSpan.Zero));

    private static AiBudget Budget(CountingLlm llm, TimeProvider time, decimal monthly = 30m) => new(
        llm,
        Options.Create(new AiOptions
        {
            MonthlyBudgetUsd = monthly,
            Prices = new(StringComparer.Ordinal)
            {
                ["cheap"] = new ModelPrice { InputPerMillionTokens = 0.10m, OutputPerMillionTokens = 0.50m },
                ["dear"] = new ModelPrice { InputPerMillionTokens = 4m, OutputPerMillionTokens = 20m },
            },
        }),
        time,
        NullLogger<AiBudget>.Instance);

    private static LlmRequest Request() => new(new RenderedPrompt(Prompt, "system", "{}"), "input");

    [Fact]
    public void TheDailyShare_IsTheMonthlyBudgetOverTheDaysInTheMonth()
    {
        var time = November();
        var budget = Budget(new CountingLlm(), time, monthly: 25m);

        budget.DailyLimitUsd().ShouldBe(25m / 30);
        time.SetUtcNow(new DateTimeOffset(2027, 2, 1, 0, 0, 0, TimeSpan.Zero));
        budget.DailyLimitUsd().ShouldBe(25m / 28);
    }

    [Fact]
    public async Task OnceTodaysShareIsSpent_NoCallIsMade_UntilTheNextUtcDay()
    {
        var time = November();
        var llm = new CountingLlm { Model = "cheap", Usage = new LlmUsage(4_000_000, 1_000_000) }; // $0.40 + $0.50 = $0.90
        var budget = Budget(llm, time);

        (await budget.CompleteAsync(Request(), Ct)).IsSuccess.ShouldBeTrue();
        (await budget.CompleteAsync(Request(), Ct)).IsSuccess.ShouldBeTrue("$0.90 spent of $1.00");
        var third = await budget.CompleteAsync(Request(), Ct);

        third.IsSuccess.ShouldBeFalse("$1.80 spent");
        third.Error.ShouldBe(AiErrors.BudgetReached);
        third.Error.Code.ShouldBe("ai.unavailable");
        llm.Calls.ShouldBe(2);

        time.SetUtcNow(new DateTimeOffset(2026, 11, 17, 0, 0, 0, TimeSpan.Zero));
        (await budget.CompleteAsync(Request(), Ct)).IsSuccess.ShouldBeTrue("a new UTC day starts a new count");
        llm.Calls.ShouldBe(3);
    }

    [Fact]
    public async Task AModelWithoutAPrice_IsPricedAtTheHighestPriceListed()
    {
        var llm = new CountingLlm { Model = "some-fallback-model", Usage = new LlmUsage(250_000, 0) }; // $1.00 at "dear"
        var budget = Budget(llm, November());

        await budget.CompleteAsync(Request(), Ct);

        budget.IsReached().ShouldBeTrue();
    }

    [Fact]
    public async Task FailedCalls_AndRepliesWithoutUsage_CostNothing()
    {
        var llm = new CountingLlm { Model = "dear", Usage = null };
        var budget = Budget(llm, November());

        await budget.CompleteAsync(Request(), Ct);
        llm.Failure = AiErrors.Unavailable;
        await budget.CompleteAsync(Request(), Ct);

        budget.IsReached().ShouldBeFalse();
        llm.Calls.ShouldBe(2);
    }

    [Fact]
    public async Task AZeroBudget_SwitchesLlmCallsOff()
    {
        var llm = new CountingLlm();
        var budget = Budget(llm, November(), monthly: 0m);

        (await budget.CompleteAsync(Request(), Ct)).Error.ShouldBe(AiErrors.BudgetReached);
        llm.Calls.ShouldBe(0);
    }

    private sealed class CountingLlm : ILlmClient
    {
        public int Calls { get; private set; }

        public string Model { get; init; } = "cheap";

        public LlmUsage? Usage { get; init; } = new(1, 1);

        public Error? Failure { get; set; }

        public Task<Result<LlmResponse>> CompleteAsync(LlmRequest request, CancellationToken ct)
        {
            Calls++;
            Result<LlmResponse> result = Failure is { } failure ? (Result<LlmResponse>)failure : new LlmResponse("{}", request.Prompt.Id, Model, Usage);
            return Task.FromResult(result);
        }
    }
}
