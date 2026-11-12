using Ithaca.Domain.Errors;

namespace Ithaca.Application.Common;

/// <summary>
/// Contract stubs (BE-01, api-contract.md §2): a service method whose backlog task hasn't landed returns
/// <see cref="NotImplemented"/> (501 <c>server.not_implemented</c>). Its signature and DTOs are final; only the behaviour is
/// missing. A feature task replaces the body; BE-17 deletes this class once nothing calls it.
/// </summary>
public static class ContractStub
{
    /// <summary>For the CA1822 suppression on stub members: they become instance members once they use their dependencies.</summary>
    public const string Justification = "Contract stub (BE-01): the instance members use injected dependencies once the feature task lands.";

    public static readonly Error NotImplemented =
        new(ErrorCodes.ServerNotImplemented, "This endpoint is part of the contract but its backlog task hasn't landed yet.");

    /// <summary>The stub answer for a use case that returns a value.</summary>
    public static Task<Result<T>> Of<T>() => Task.FromResult(Result<T>.Failure(NotImplemented));

    /// <summary>The stub answer for a command that returns nothing.</summary>
    public static Task<Result> Command() => Task.FromResult(Result.Failure(NotImplemented));
}
