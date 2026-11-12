using System.Text.Json;
using Ithaca.Application.Common;

namespace Ithaca.Application.Ai;

/// <summary>The one place LLM JSON becomes a typed DTO. Malformed output is an <see cref="Error"/>, never an exception to the user.</summary>
public static class StructuredOutput
{
    public const string InvalidOutputCode = Domain.Errors.ErrorCodes.AiInvalidOutput;

    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    public static Result<T> Parse<T>(LlmResponse response)
        where T : class
    {
        ArgumentNullException.ThrowIfNull(response);
        try
        {
            return JsonSerializer.Deserialize<T>(response.Json, Options) is { } value
                ? value
                : AiErrors.InvalidOutput(response.PromptId, "an empty object");
        }
        catch (JsonException)
        {
            return AiErrors.InvalidOutput(response.PromptId, "invalid JSON");
        }
    }
}
