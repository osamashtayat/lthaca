using Ithaca.Api.Errors;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace Ithaca.Api.OpenApi;

/// <summary>
/// The OpenAPI document is the executable contract (contracts/openapi.json, written by scripts/export-openapi.sh) and the
/// source of the frontend's generated types. These transformers make it say what the API really sends.
/// </summary>
public static class OpenApiSetup
{
    public const string DocumentName = "v1";

    public static IServiceCollection AddIthacaOpenApi(this IServiceCollection services) =>
        services.AddOpenApi(DocumentName, options =>
        {
            options.AddDocumentTransformer((document, _, _) =>
            {
                document.Info = new OpenApiInfo { Title = "Ithaca API", Version = "v1", Description = "Generated from the API." };
                // Server URLs depend on the host that served the document; leaving them out keeps the export deterministic.
                document.Servers = [];
                return Task.CompletedTask;
            });
            options.AddSchemaTransformer(DescribeProblemExtensions);
            options.AddSchemaTransformer(RequireAllResponseProperties);
        });

    /// <summary>Every ProblemDetails we send carries <c>code</c> and <c>traceId</c>.</summary>
    private static Task DescribeProblemExtensions(OpenApiSchema schema, OpenApiSchemaTransformerContext context, CancellationToken _)
    {
        if (!typeof(ProblemDetails).IsAssignableFrom(context.JsonTypeInfo.Type) || schema.Properties is null)
        {
            return Task.CompletedTask;
        }

        schema.Properties[ProblemDetailsSetup.CodeKey] = new OpenApiSchema { Type = JsonSchemaType.String, Description = "Stable error code, e.g. film.not_found." };
        schema.Properties[ProblemDetailsSetup.TraceIdKey] = new OpenApiSchema { Type = JsonSchemaType.String };
        schema.Required ??= new HashSet<string>(StringComparer.Ordinal);
        schema.Required.Add(ProblemDetailsSetup.CodeKey);
        schema.Required.Add(ProblemDetailsSetup.TraceIdKey);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Response DTOs (<c>…Dto</c>) always send every property (nullable ones as <c>null</c>), so mark them all required;
    /// otherwise generated TypeScript would make every field optional.
    /// </summary>
    private static Task RequireAllResponseProperties(OpenApiSchema schema, OpenApiSchemaTransformerContext context, CancellationToken _)
    {
        var type = context.JsonTypeInfo.Type;
        if (context.JsonPropertyInfo is not null || !type.Name.EndsWith("Dto", StringComparison.Ordinal) || schema.Properties is not { Count: > 0 })
        {
            return Task.CompletedTask;
        }

        schema.Required = new HashSet<string>(schema.Properties.Keys, StringComparer.Ordinal);
        return Task.CompletedTask;
    }
}
