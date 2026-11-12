using Ithaca.Application.Ai;
using Ithaca.Infrastructure.Ai;

namespace Ithaca.Tests.Ai;

/// <summary>What the Anthropic client sends, checked without calling the API.</summary>
public sealed class AnthropicRequestTests
{
    private static readonly RenderedPrompt Prompt = new(Prompts.MoodParse, "system text", """{"type":"object","properties":{},"required":[],"additionalProperties":false}""");

    [Fact]
    public void Request_CarriesSystemPromptInputSchemaAndFallback()
    {
        var parameters = AnthropicLlmClient.BuildParameters(new LlmRequest(Prompt, "nothing scary"), "claude-opus-5-5", new AiOptions());

        parameters.Model.ToString().ShouldContain("claude-opus-5-5");
        parameters.System!.ToString()!.ShouldContain("system text");
        parameters.OutputConfig!.Format.ShouldNotBeNull();
        parameters.Fallbacks.ShouldNotBeNull();
        parameters.Betas!.Select(b => b.ToString()).ShouldContain(b => b.Contains("server-side-fallback", StringComparison.Ordinal));
    }

    [Fact]
    public void Request_WithoutFallback_SendsNoBeta()
    {
        var parameters = AnthropicLlmClient.BuildParameters(new LlmRequest(Prompt, "x"), "claude-haiku-5-5", new AiOptions { UseRefusalFallback = false });

        parameters.Fallbacks.ShouldBeNull();
        parameters.Betas.ShouldBeNull();
    }

    [Fact]
    public void ModelFor_UsesPerPromptOverride()
    {
        var options = new AiOptions { ModelOverrides = new() { ["mood-parse"] = "claude-haiku-5-5" } };

        options.ModelFor(Prompts.MoodParse).ShouldBe("claude-haiku-5-5");
        options.ModelFor(Prompts.RankCandidates).ShouldBe(options.LlmModel);
    }
}

/// <summary>How the Anthropic client turns a reply into a Result, without calling the API.</summary>
public sealed class AnthropicReplyTests
{
    [Fact]
    public void Refusal_IsRefused() =>
        AnthropicLlmClient.MapReply(Prompts.MoodParse, "refusal", "", "m").Error.ShouldBe(AiErrors.Refused);

    [Fact]
    public void MaxTokens_IsInvalidOutput() =>
        AnthropicLlmClient.MapReply(Prompts.MoodParse, "max_tokens", "{\"a\":", "m").Error!.Code.ShouldBe(StructuredOutput.InvalidOutputCode);

    [Fact]
    public void EmptyText_IsInvalidOutput() =>
        AnthropicLlmClient.MapReply(Prompts.MoodParse, "end_turn", " ", "m").Error!.Code.ShouldBe(StructuredOutput.InvalidOutputCode);

    [Fact]
    public void Text_IsTheResponse()
    {
        var result = AnthropicLlmClient.MapReply(Prompts.MoodParse, "end_turn", "{}", "claude-opus-5-5");

        result.Value.ShouldBe(new LlmResponse("{}", Prompts.MoodParse, "claude-opus-5-5"));
    }
}
