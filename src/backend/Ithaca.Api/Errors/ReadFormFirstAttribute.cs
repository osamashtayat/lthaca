using Microsoft.AspNetCore.Mvc.Filters;

namespace Ithaca.Api.Errors;

/// <summary>
/// Reads the form before model binding on an upload action. MVC's form binding turns a body over
/// <c>[RequestSizeLimit]</c> into a 400 <c>validation</c> error with an empty field; reading it here first lets that
/// failure reach <see cref="ProblemDetailsExceptionHandler"/>, which answers 413 <c>request.too_large</c> (api-contract.md
/// §2). Model binding then uses the form already read. Pair it with <c>[RequestSizeLimit]</c>.
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class ReadFormFirstAttribute : Attribute, IAsyncResourceFilter
{
    public async Task OnResourceExecutionAsync(ResourceExecutingContext context, ResourceExecutionDelegate next)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(next);
        var request = context.HttpContext.Request;
        if (request.HasFormContentType)
        {
            await request.ReadFormAsync(context.HttpContext.RequestAborted);
        }

        await next();
    }
}
