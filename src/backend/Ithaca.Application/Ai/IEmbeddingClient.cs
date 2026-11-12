namespace Ithaca.Application.Ai;

/// <summary>Turns text into a vector of <c>Film.EmbeddingDimensions</c> floats. Chosen by <see cref="AiOptions.EmbeddingProvider"/>.</summary>
public interface IEmbeddingClient
{
    Task<float[]> EmbedAsync(string text, CancellationToken ct);
}
