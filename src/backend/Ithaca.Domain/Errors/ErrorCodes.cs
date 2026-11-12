namespace Ithaca.Domain.Errors;

/// <summary>
/// Every stable, machine-readable error code, <c>area.snake_case</c>. Adding a code means updating this file,
/// <c>Ithaca.Api/Errors/ErrorStatusCodes.cs</c> (a test fails otherwise) and <c>src/frontend/src/shared/api/problem.ts</c>.
/// </summary>
public static class ErrorCodes
{
    public const string Validation = "validation";
    public const string NotFound = "not_found";
    public const string Conflict = "conflict";
    public const string ServerError = "server.error";

    public const string FilmNotFound = "film.not_found";

    public const string AiInvalidOutput = "ai.invalid_output";
    public const string AiUnavailable = "ai.unavailable";
    public const string AiRefused = "ai.refused";

    public const string TmdbUnavailable = "tmdb.unavailable";
}
