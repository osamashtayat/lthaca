using System.Security.Cryptography;
using System.Text;
using Ithaca.Application.Ai;
using Ithaca.Domain.Films;

namespace Ithaca.Infrastructure.Ai;

/// <summary>
/// Offline embedding stand-in (<c>Ai:EmbeddingProvider = Fake</c>): a deterministic unit vector seeded from the text's
/// hash. Same text → same vector, so similarity code can be tested; the numbers mean nothing.
/// </summary>
internal sealed class FakeEmbeddingClient : IEmbeddingClient
{
    public Task<float[]> EmbedAsync(string text, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(text);
        var seed = BitConverter.ToInt32(SHA256.HashData(Encoding.UTF8.GetBytes(text)), 0);
#pragma warning disable CA5394 // Not security-relevant: a reproducible fake vector.
        var random = new Random(seed);
        var vector = new float[Film.EmbeddingDimensions];
        for (var i = 0; i < vector.Length; i++)
        {
            vector[i] = (float)(random.NextDouble() * 2 - 1);
        }
#pragma warning restore CA5394

        var norm = MathF.Sqrt(vector.Sum(v => v * v));
        for (var i = 0; i < vector.Length; i++)
        {
            vector[i] /= norm;
        }

        return Task.FromResult(vector);
    }
}
