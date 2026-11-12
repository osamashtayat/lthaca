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

        // Keys are either quoted ('film.not_found':) or bare identifiers (validation:).
        codes.ShouldAllBe(
            code => problemTs.Contains($"'{code}':", StringComparison.Ordinal) || problemTs.Contains($"\n  {code}:", StringComparison.Ordinal),
            "Add a user message to src/frontend/src/shared/api/problem.ts for every code.");
    }
}
