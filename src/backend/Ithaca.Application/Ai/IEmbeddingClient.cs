namespace Ithaca.Application.Ai;

/// <summary>Turns text into a vector of <c>Film.EmbeddingDimensions</c> floats. Chosen by <see cref="AiOptions.EmbeddingProvider"/>.</summary>
public interface IEmbeddingClient
{
    /// <summary>Stored next to each vector (<c>films.embedding_model</c>), e.g. <c>voyage-4</c> or <c>fake</c>.</summary>
    string ModelName { get; }

    Task<float[]> EmbedAsync(string text, CancellationToken ct);
}
