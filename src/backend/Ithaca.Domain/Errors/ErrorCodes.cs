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

    /// <summary>Contract stub (BE-01): the endpoint exists, its backlog task hasn't landed. Deleted by BE-17.</summary>
    public const string ServerNotImplemented = "server.not_implemented";

    public const string AuthAntiforgeryFailed = "auth.antiforgery_failed";
    public const string AuthUnauthenticated = "auth.unauthenticated";
    public const string AuthInvalidCredentials = "auth.invalid_credentials";
    public const string AuthForbidden = "auth.forbidden";
    public const string AuthEmailTaken = "auth.email_taken";

    public const string FilmNotFound = "film.not_found";

    public const string DeckNotFound = "deck.not_found";
    public const string DeckItemNotFound = "deck.item_not_found";
    public const string DeckAlreadySwiped = "deck.already_swiped";
    public const string DeckNoYeses = "deck.no_yeses";

    public const string RoomNotFound = "room.not_found";
    public const string RoomNotMember = "room.not_member";
    public const string RoomNotHost = "room.not_host";
    public const string RoomAlreadyVoted = "room.already_voted";
    public const string RoomClosed = "room.closed";
    public const string RoomFull = "room.full";
    public const string RoomInvalidState = "room.invalid_state";
    public const string RoomNoResult = "room.no_result";

    /// <summary>Mood text isn't a mood (unclear or off-topic) and no chips were tapped; nothing is created (ADR-0022).</summary>
    public const string MoodUnclear = "mood.unclear";

    public const string AiInvalidOutput = "ai.invalid_output";
    public const string AiUnavailable = "ai.unavailable";
    public const string AiRefused = "ai.refused";

    public const string TmdbUnavailable = "tmdb.unavailable";
}
