using Ithaca.Domain.Errors;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace Ithaca.Api.Errors;

/// <summary>
/// The one global exception handler. <see cref="DomainException"/> (a broken entity invariant) keeps its code; anything
/// else is a bug or an infrastructure fault and becomes a 500 <c>server.error</c> with no internals. Expected failures
/// don't come through here: services return them as Result errors (ResultExtensions).
/// </summary>
public sealed partial class ProblemDetailsExceptionHandler(
    IProblemDetailsService problemDetails, ILogger<ProblemDetailsExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(httpContext);
        ArgumentNullException.ThrowIfNull(exception);

        var (code, detail) = exception is DomainException e ? (e.Code, e.Message) : (ErrorCodes.ServerError, "An unexpected error occurred.");
        var status = ErrorStatusCodes.For(code);

        if (status >= StatusCodes.Status500InternalServerError)
        {
            LogUnhandled(logger, exception, code);
            code = ErrorCodes.ServerError;
            detail = "An unexpected error occurred.";
        }
        else
        {
            // Messages may echo user input, so only the code is logged.
            LogHandled(logger, code, status);
        }

        httpContext.Response.StatusCode = status;
        ProblemDetails problem = exception is DomainException { Field: { } field } && status < StatusCodes.Status500InternalServerError
            ? new HttpValidationProblemDetails(new Dictionary<string, string[]>(StringComparer.Ordinal) { [field] = [detail] })
            : new ProblemDetails();
        problem.Status = status;
        problem.Detail = detail;
        problem.Extensions[ProblemDetailsSetup.CodeKey] = code;
        return await problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails = problem,
        });
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Unhandled exception mapped to {Code}")]
    private static partial void LogUnhandled(ILogger logger, Exception exception, string code);

    [LoggerMessage(Level = LogLevel.Information, Message = "Request failed with {Code} ({Status})")]
    private static partial void LogHandled(ILogger logger, string code, int status);
}
