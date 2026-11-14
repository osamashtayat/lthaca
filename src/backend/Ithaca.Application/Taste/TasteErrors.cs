using Ithaca.Application.Common;
using Ithaca.Domain.Errors;

namespace Ithaca.Application.Taste;

/// <summary>The taste errors, defined once (api-contract.md §2, §12).</summary>
public static class TasteErrors
{
    /// <summary>A deck (or a room, for its host) by taste was asked for without enough history.</summary>
    public static readonly Error NotReady = new(ErrorCodes.TasteNotReady, "By taste needs a few more films you love.");
}
