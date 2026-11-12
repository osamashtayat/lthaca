namespace Ithaca.Domain.Errors;

/// <summary>
/// An invariant broken inside an entity (e.g. a constructor given an impossible value). Entities can't return a
/// <c>Result</c>, so they throw this; the Api's global handler maps <see cref="Code"/> to a ProblemDetails response.
/// Expected, user-facing failures in use cases are returned as <c>Result</c> errors instead (docs/backend-conventions.md).
/// </summary>
public sealed class DomainException(string code, string message, string? field = null) : Exception(message)
{
    public string Code { get; } = code;

    /// <summary>The camelCase input field the failure is about (becomes <c>errors.{field}</c>), if known.</summary>
    public string? Field { get; } = field;
}
