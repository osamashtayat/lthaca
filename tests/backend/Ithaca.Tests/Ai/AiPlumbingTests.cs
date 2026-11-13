using Ithaca.Application.Ai;
using Ithaca.Domain.Films;
using Ithaca.Infrastructure.Ai;
using Ithaca.Tests.Harness;
using Microsoft.Extensions.Options;

namespace Ithaca.Tests.Ai;

public sealed class AiPlumbingTests
{
    private static FilePromptLibrary Library() =>
        new(Options.Create(new AiOptions { PromptsPath = RepoPaths.File("prompts") }));

    [Fact]
    public async Task EveryDeclaredPrompt_HasAFile()
    {
        foreach (var id in new[] { Prompts.MoodParse, Prompts.RankCandidates, Prompts.ExplainPick })
        {
            var prompt = await Library().RenderAsync(id, new Dictionary<string, string>(), TestContext.Current.CancellationToken);
            prompt.Text.ShouldNotBeNullOrWhiteSpace();
            using var schema = System.Text.Json.JsonDocument.Parse(prompt.OutputSchemaJson);
            schema.RootElement.GetProperty("additionalProperties").GetBoolean().ShouldBeFalse("structured outputs need closed schemas");
        }
    }

    [Fact]
    public async Task RenderAsync_FillsVariables()
    {
        var prompt = await Library().RenderAsync(Prompts.MoodParse, new Dictionary<string, string> { ["genres"] = "Comedy, Horror" }, TestContext.Current.CancellationToken);

        prompt.Text.ShouldContain("Comedy, Horror");
        prompt.Text.ShouldNotContain("{{genres}}");
    }

    [Fact]
    public async Task FakeLlm_RankCandidates_ReturnsOnlyGivenIds()
    {
        var prompt = await Library().RenderAsync(Prompts.RankCandidates, new Dictionary<string, string>(), TestContext.Current.CancellationToken);
        var response = await new FakeLlmClient().CompleteAsync(new LlmRequest(prompt, "[550,27205]"), TestContext.Current.CancellationToken);

        response.IsSuccess.ShouldBeTrue();
        var parsed = StructuredOutput.Parse<RankCandidatesOutput>(response.Value);

        parsed.IsSuccess.ShouldBeTrue();
        parsed.Value.Ranked.Select(r => r.TmdbId).ShouldBe([550, 27205]);
    }

    [Fact]
    public void StructuredOutput_InvalidJson_IsAnError()
    {
        var parsed = StructuredOutput.Parse<MoodParseOutput>(new LlmResponse("not json", Prompts.MoodParse, "fake"));

        parsed.IsSuccess.ShouldBeFalse();
        parsed.Error!.Code.ShouldBe(StructuredOutput.InvalidOutputCode);
    }

    [Fact]
    public async Task FakeEmbedding_IsDeterministicAndRightSized()
    {
        var client = new FakeEmbeddingClient();
        var vectors = await client.EmbedAsync(["cosy rainy-day film", "cosy rainy-day film", "heist"], EmbeddingInputType.Query, TestContext.Current.CancellationToken);

        vectors.Value.Count.ShouldBe(3);
        vectors.Value[0].Length.ShouldBe(Film.EmbeddingDimensions);
        vectors.Value[0].ShouldBe(vectors.Value[1]);
        vectors.Value[0].ShouldNotBe(vectors.Value[2]);
    }
}
