using Ithaca.Application.Common;
using Ithaca.Domain.Errors;

namespace Ithaca.Application.Imports;

/// <summary>The import errors, defined once (api-contract.md §2, §13). Another user's import is also <see cref="NotFound"/>.</summary>
public static class ImportErrors
{
    public static readonly Error NotFound = Error.NotFound(ErrorCodes.ImportNotFound, "No such import.");

    /// <summary>Not a Letterboxd export: wrong type, unreadable, no recognised file or header, or a ZIP over the entry/ratio limits.</summary>
    public static readonly Error InvalidFile = new(ErrorCodes.ImportInvalidFile, "That isn't a Letterboxd export.");

    public static readonly Error AlreadyRunning = Error.Conflict(ErrorCodes.ImportAlreadyRunning, "An import is already running.");

    /// <summary>The files read from the upload unpack to more than the limit.</summary>
    public static readonly Error FileTooLarge = new(ErrorCodes.ImportFileTooLarge, "The export is too large to import.");

    public static readonly Error NothingToImport = new(ErrorCodes.ImportNothingToImport, "The export has no films to import.");

    public static readonly Error TooManyFilms = new(ErrorCodes.ImportTooManyFilms, "The export has more films than one import takes.");
}
