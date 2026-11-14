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
        [ErrorCodes.MoodUnclear] = StatusCodes.Status422UnprocessableEntity,
        [ErrorCodes.AiInvalidOutput] = StatusCodes.Status502BadGateway,
        [ErrorCodes.AiUnavailable] = StatusCodes.Status503ServiceUnavailable,
        [ErrorCodes.AiRefused] = StatusCodes.Status422UnprocessableEntity,
        [ErrorCodes.TmdbUnavailable] = StatusCodes.Status503ServiceUnavailable,
        [ErrorCodes.WikidataUnavailable] = StatusCodes.Status503ServiceUnavailable,
        [ErrorCodes.ServerError] = StatusCodes.Status500InternalServerError,
        [ErrorCodes.ServerNotImplemented] = StatusCodes.Status501NotImplemented,
        [ErrorCodes.AuthAntiforgeryFailed] = StatusCodes.Status400BadRequest,
        [ErrorCodes.AuthUnauthenticated] = StatusCodes.Status401Unauthorized,
        [ErrorCodes.AuthInvalidCredentials] = StatusCodes.Status401Unauthorized,
        [ErrorCodes.AuthForbidden] = StatusCodes.Status403Forbidden,
        [ErrorCodes.AuthEmailTaken] = StatusCodes.Status409Conflict,
        [ErrorCodes.DeckNotFound] = StatusCodes.Status404NotFound,
        [ErrorCodes.DeckItemNotFound] = StatusCodes.Status404NotFound,
        [ErrorCodes.DeckAlreadySwiped] = StatusCodes.Status409Conflict,
        [ErrorCodes.DeckNoYeses] = StatusCodes.Status422UnprocessableEntity,
        [ErrorCodes.TasteNotReady] = StatusCodes.Status422UnprocessableEntity,
        [ErrorCodes.RequestTooLarge] = StatusCodes.Status413PayloadTooLarge,
        [ErrorCodes.ImportNotFound] = StatusCodes.Status404NotFound,
        [ErrorCodes.ImportInvalidFile] = StatusCodes.Status400BadRequest,
        [ErrorCodes.ImportAlreadyRunning] = StatusCodes.Status409Conflict,
        [ErrorCodes.ImportFileTooLarge] = StatusCodes.Status413PayloadTooLarge,
        [ErrorCodes.ImportNothingToImport] = StatusCodes.Status422UnprocessableEntity,
        [ErrorCodes.ImportTooManyFilms] = StatusCodes.Status422UnprocessableEntity,
        [ErrorCodes.RoomNotFound] = StatusCodes.Status404NotFound,
        [ErrorCodes.RoomNotMember] = StatusCodes.Status403Forbidden,
        [ErrorCodes.RoomNotHost] = StatusCodes.Status403Forbidden,
        [ErrorCodes.RoomAlreadyVoted] = StatusCodes.Status409Conflict,
        [ErrorCodes.RoomClosed] = StatusCodes.Status409Conflict,
        [ErrorCodes.RoomFull] = StatusCodes.Status409Conflict,
        [ErrorCodes.RoomInvalidState] = StatusCodes.Status409Conflict,
        [ErrorCodes.RoomNoResult] = StatusCodes.Status409Conflict,
    };

    public static IReadOnlyCollection<string> MappedCodes => StatusByCode.Keys;

    /// <summary>Unknown codes are a programming error and surface as 500, never as a guessed 4xx.</summary>
    public static int For(string code) =>
        StatusByCode.TryGetValue(code, out var status) ? status : StatusCodes.Status500InternalServerError;

    /// <summary>Code for problems the framework produces itself (model validation, unknown routes).</summary>
    public static string DefaultCodeFor(int status) => status switch
    {
        StatusCodes.Status400BadRequest => ErrorCodes.Validation,
        StatusCodes.Status401Unauthorized => ErrorCodes.AuthUnauthenticated,
        StatusCodes.Status403Forbidden => ErrorCodes.AuthForbidden,
        StatusCodes.Status404NotFound => ErrorCodes.NotFound,
        StatusCodes.Status409Conflict => ErrorCodes.Conflict,
        >= 500 => ErrorCodes.ServerError,
        _ => "http." + status.ToString(System.Globalization.CultureInfo.InvariantCulture),
    };
}
