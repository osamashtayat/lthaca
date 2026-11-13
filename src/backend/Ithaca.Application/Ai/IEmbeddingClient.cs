using Ithaca.Application.Common;

namespace Ithaca.Application.Ai;

/// <summary>What a text is for: Voyage embeds a film (<see cref="Document"/>) and a search for one (<see cref="Query"/>) differently.</summary>
public enum EmbeddingInputType
{
    /// <summary>A film's Wikidata text (the catalog sync).</summary>
    Document,

    /// <summary>Mood or taste text compared against films (BE-09, BE-14).</summary>
    Query,
}

/// <summary>
/// Turns texts into vectors of <c>Film.EmbeddingDimensions</c> floats. Chosen by <see cref="AiOptions.EmbeddingProvider"/>.
/// Never give it TMDB-derived text (ADR-0017). Failures are <see cref="AiErrors.Unavailable"/> (provider down, rate limited
/// after back-off, bad key) or <c>ai.invalid_output</c> (wrong number or size of vectors), never exceptions.
/// </summary>
public interface IEmbeddingClient
{
    /// <summary>Stored next to each vector (<c>films.embedding_model</c>), e.g. <c>voyage-4</c> or <c>fake</c>.</summary>
    string ModelName { get; }

    /// <summary>One vector per text, in the same order. Any number of texts; the client splits them into requests.</summary>
    Task<Result<IReadOnlyList<float[]>>> EmbedAsync(IReadOnlyList<string> texts, EmbeddingInputType inputType, CancellationToken ct);
}
