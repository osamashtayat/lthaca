using Ithaca.Domain.Errors;

namespace Ithaca.Application.Common;

/// <summary>
/// An expected failure of a use case: a stable <see cref="Code"/> from <see cref="ErrorCodes"/>, a safe message and,
/// for validation, the camelCase field it concerns. The Api derives the HTTP status from the code (ErrorStatusCodes).
/// </summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Naming", "CA1716", Justification = "C#-only codebase; Error is the conventional name.")]
public sealed record Error(string Code, string Message, string? Field = null)
{
    public static Error NotFound(string code = ErrorCodes.NotFound, string message = "The resource was not found.") =>
        new(code, message);

    public static Error Validation(string field, string message) => new(ErrorCodes.Validation, message, field);

    public static Error Conflict(string code, string message) => new(code, message);
}
