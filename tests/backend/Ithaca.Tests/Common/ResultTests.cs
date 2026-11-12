using Ithaca.Application.Common;

namespace Ithaca.Tests.Common;

public sealed class ResultTests
{
    [Fact]
    public void ValueOfFailure_Throws()
    {
        Result<int> result = Error.NotFound();

        result.IsSuccess.ShouldBeFalse();
        Should.Throw<InvalidOperationException>(() => result.Value);
    }

    [Fact]
    public void ImplicitValue_IsSuccess()
    {
        Result<string> result = "ok";

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBe("ok");
    }
}
