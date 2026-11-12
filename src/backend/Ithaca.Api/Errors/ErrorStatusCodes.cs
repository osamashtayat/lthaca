using Ithaca.Domain.Errors;

namespace Ithaca.Api.Errors;

/// <summary>
/// The single code → HTTP status table. The status is derived from the error code, never from the type that carried it,
/// so Result errors and DomainExceptions map identically. A test asserts every <see cref="ErrorCodes"/> constant is here.
/// </summary>
public static class ErrorStatusCodes
{
    private static readonly Dictionary<string, int> StatusByCode = new(StringComparer.Ordinal)
    {
        [ErrorCodes.Validation] = StatusCodes.Status400BadRequest,
        [ErrorCodes.NotFound] = StatusCodes.Status404NotFound,
        [ErrorCodes.FilmNotFound] = StatusCodes.Status404NotFound,
        [ErrorCodes.Conflict] = StatusCodes.Status409Conflict,
        [ErrorCodes.AiInvalidOutput] = StatusCodes.Status502BadGateway,
        [ErrorCodes.ServerError] = StatusCodes.Status500InternalServerError,
    };

    public static IReadOnlyCollection<string> MappedCodes => StatusByCode.Keys;

    /// <summary>Unknown codes are a programming error and surface as 500, never as a guessed 4xx.</summary>
    public static int For(string code) =>
        StatusByCode.TryGetValue(code, out var status) ? status : StatusCodes.Status500InternalServerError;

    /// <summary>Code for problems the framework produces itself (model validation, unknown routes).</summary>
    public static string DefaultCodeFor(int status) => status switch
    {
        StatusCodes.Status400BadRequest => ErrorCodes.Validation,
        StatusCodes.Status404NotFound => ErrorCodes.NotFound,
        StatusCodes.Status409Conflict => ErrorCodes.Conflict,
        >= 500 => ErrorCodes.ServerError,
        _ => "http." + status.ToString(System.Globalization.CultureInfo.InvariantCulture),
    };
}
