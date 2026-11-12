using Ithaca.Application.Common;
using Ithaca.Domain.Errors;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace Ithaca.Api.Errors;

/// <summary>
/// The ONLY place a <see cref="Result"/> becomes an HTTP response. Controllers end with <c>this.ToActionResult(result)</c>
/// (or <c>ToCreatedResult</c>) and never inspect <c>IsSuccess</c> themselves.
/// </summary>
public static class ResultExtensions
{
    public static ActionResult<T> ToActionResult<T>(this ControllerBase controller, Result<T> result)
    {
        ArgumentNullException.ThrowIfNull(controller);
        ArgumentNullException.ThrowIfNull(result);
        return result.IsSuccess ? controller.Ok(result.Value) : controller.ToProblem(result.Error);
    }

    /// <summary>201 with a Location header built from the created value.</summary>
    public static ActionResult<T> ToCreatedResult<T>(this ControllerBase controller, Result<T> result, Func<T, string?> location)
    {
        ArgumentNullException.ThrowIfNull(controller);
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(location);
        return result.IsSuccess ? controller.Created(location(result.Value), result.Value) : controller.ToProblem(result.Error);
    }

    /// <summary>204 for commands that return nothing.</summary>
    public static ActionResult ToActionResult(this ControllerBase controller, Result result)
    {
        ArgumentNullException.ThrowIfNull(controller);
        ArgumentNullException.ThrowIfNull(result);
        return result.IsSuccess ? controller.NoContent() : controller.ToProblem(result.Error);
    }

    private static ObjectResult ToProblem(this ControllerBase controller, Error error) => controller.HttpContext.ToProblemResult(error);

    /// <summary>
    /// The ProblemDetails response for an error, also for filters that have no controller (antiforgery). Unmapped codes
    /// are a bug and hide behind 500 <c>server.error</c>, like the exception handler does.
    /// </summary>
    public static ObjectResult ToProblemResult(this HttpContext http, Error error)
    {
        ArgumentNullException.ThrowIfNull(http);
        ArgumentNullException.ThrowIfNull(error);
        var status = ErrorStatusCodes.For(error.Code);
        var hidden = status == StatusCodes.Status500InternalServerError;
        var code = hidden ? ErrorCodes.ServerError : error.Code;
        var detail = hidden ? "An unexpected error occurred." : error.Message;
        http.Items[ProblemDetailsSetup.CodeItemKey] = code;

        var factory = http.RequestServices.GetRequiredService<ProblemDetailsFactory>();
        ProblemDetails problem;
        if (error.Field is not null && status == StatusCodes.Status400BadRequest)
        {
            var modelState = new ModelStateDictionary();
            modelState.AddModelError(error.Field, error.Message);
            problem = factory.CreateValidationProblemDetails(http, modelState, status, detail: detail);
        }
        else
        {
            problem = factory.CreateProblemDetails(http, status, detail: detail);
        }

        return new ObjectResult(problem) { StatusCode = status, ContentTypes = { "application/problem+json" } };
    }
}
