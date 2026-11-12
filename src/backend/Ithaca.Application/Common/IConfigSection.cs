namespace Ithaca.Application.Common;

/// <summary>
/// Marks an options class bound from configuration. Every options class declares its section here and is registered
/// with <c>AddValidatedOptions&lt;T&gt;()</c> (Infrastructure/Configuration): bound, DataAnnotations-validated, checked
/// at startup. That is the one options pattern (docs/backend-conventions.md).
/// </summary>
public interface IConfigSection
{
    static abstract string SectionName { get; }
}
