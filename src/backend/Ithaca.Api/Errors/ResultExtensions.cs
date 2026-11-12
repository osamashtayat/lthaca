using Ithaca.Application.Common;
using Ithaca.Domain.Errors;
using Microsoft.AspNetCore.Mvc;
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

    private static ActionResult ToProblem(this ControllerBase controller, Error error)
    {
        var status = ErrorStatusCodes.For(error.Code);
        // An unmapped code is a bug: hide it behind server.error, like the exception handler does.
        var hidden = status == StatusCodes.Status500InternalServerError;
        var code = hidden ? ErrorCodes.ServerError : error.Code;
        var detail = hidden ? "An unexpected error occurred." : error.Message;
        controller.HttpContext.Items[ProblemDetailsSetup.CodeItemKey] = code;

        if (error.Field is not null && status == StatusCodes.Status400BadRequest)
        {
            var modelState = new ModelStateDictionary();
            modelState.AddModelError(error.Field, error.Message);
            return controller.ValidationProblem(detail: detail, statusCode: status, modelStateDictionary: modelState);
        }

        return controller.Problem(detail: detail, statusCode: status);
    }
}
