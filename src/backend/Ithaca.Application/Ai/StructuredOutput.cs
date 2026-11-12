using System.Text.Json;
using Ithaca.Application.Common;
using Ithaca.Domain.Errors;

namespace Ithaca.Application.Ai;

/// <summary>The one place LLM JSON becomes a typed DTO. Malformed output is an <see cref="Error"/>, never an exception to the user.</summary>
public static class StructuredOutput
{
    public const string InvalidOutputCode = ErrorCodes.AiInvalidOutput;

    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    public static Result<T> Parse<T>(LlmResponse response)
        where T : class
    {
        ArgumentNullException.ThrowIfNull(response);
        try
        {
            return JsonSerializer.Deserialize<T>(response.Json, Options) is { } value
                ? value
                : new Error(InvalidOutputCode, $"Prompt {response.PromptId} returned an empty object.");
        }
        catch (JsonException)
        {
            return new Error(InvalidOutputCode, $"Prompt {response.PromptId} returned invalid JSON.");
        }
    }
}
