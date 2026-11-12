using System.Reflection;
using Ithaca.Api.Errors;
using Ithaca.Domain.Errors;

namespace Ithaca.Tests.Errors;

public sealed class ErrorStatusCodesTests
{
    [Fact]
    public void EveryErrorCode_HasAnHttpStatus()
    {
        var codes = typeof(ErrorCodes).GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(f => f.IsLiteral).Select(f => (string)f.GetRawConstantValue()!);

        codes.ShouldBe(ErrorStatusCodes.MappedCodes, ignoreOrder: true);
    }

    [Fact]
    public void FrontendProblemMessages_CoverEveryErrorCode()
    {
        var problemTs = File.ReadAllText(Harness.RepoPaths.File("src", "frontend", "src", "shared", "api", "problem.ts"));
        var codes = typeof(ErrorCodes).GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(f => f.IsLiteral).Select(f => (string)f.GetRawConstantValue()!);

        codes.ShouldAllBe(code => problemTs.Contains($"'{code}'", StringComparison.Ordinal), "Add a user message to problem.ts for every code.");
    }
}
