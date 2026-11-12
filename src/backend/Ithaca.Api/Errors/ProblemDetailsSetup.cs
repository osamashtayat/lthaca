using System.Diagnostics;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.WebUtilities;

namespace Ithaca.Api.Errors;

/// <summary>
/// Gives every ProblemDetails response (from a Result error, an exception or the framework) the same shape:
/// <c>type</c> from the code, <c>title</c>, <c>code</c> and <c>traceId</c>. The frontend relies on <c>code</c>.
/// </summary>
public static class ProblemDetailsSetup
{
    public const string CodeKey = "code";
    public const string TraceIdKey = "traceId";
    public const string TypeBaseUri = "https://ithaca.local/problems/";

    /// <summary>Set by <see cref="ResultExtensions"/> so the code survives MVC's ProblemDetailsFactory.</summary>
    internal const string CodeItemKey = "ithaca.error-code";

    public static IServiceCollection AddIthacaProblemDetails(this IServiceCollection services)
    {
        services.AddProblemDetails(options => options.CustomizeProblemDetails = Customize);
        services.AddExceptionHandler<ProblemDetailsExceptionHandler>();
        return services;
    }

    private static void Customize(ProblemDetailsContext context)
    {
        var problem = context.ProblemDetails;
        var status = problem.Status ?? context.HttpContext.Response.StatusCode;
        problem.Status = status;

        if (!problem.Extensions.TryGetValue(CodeKey, out var existing) || existing is not string)
        {
            problem.Extensions[CodeKey] = context.HttpContext.Items[CodeItemKey] as string ?? ErrorStatusCodes.DefaultCodeFor(status);
        }

        var code = (string)problem.Extensions[CodeKey]!;
        problem.Type = TypeBaseUri + code;
        problem.Title ??= ReasonPhrases.GetReasonPhrase(status);
        problem.Instance = null;
        problem.Extensions.Remove("exception");
        problem.Extensions[TraceIdKey] =
            Activity.Current?.Id
            ?? context.HttpContext.Features.Get<IHttpActivityFeature>()?.Activity.Id
            ?? context.HttpContext.TraceIdentifier;
    }
}
