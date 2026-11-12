using System.Text.Json;
using Microsoft.AspNetCore.Mvc.ModelBinding.Metadata;

namespace Ithaca.Api;

/// <summary>
/// Query-bound records (<c>PageRequest</c> and the list requests deriving from it) bind camelCase names, like the JSON
/// wire format: <c>?page=2&amp;pageSize=50&amp;hideSeen=true</c>. ApiExplorer reads the same name, so the OpenAPI
/// parameters and validation error keys are camelCase too. JSON bodies are unaffected (System.Text.Json names them).
/// </summary>
public sealed class CamelCaseBindingMetadataProvider : IBindingMetadataProvider
{
    public void CreateBindingMetadata(BindingMetadataProviderContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (context.Key.MetadataKind == ModelMetadataKind.Property && context.BindingMetadata.BinderModelName is null && context.Key.Name is { } name)
        {
            context.BindingMetadata.BinderModelName = JsonNamingPolicy.CamelCase.ConvertName(name);
        }
    }
}
