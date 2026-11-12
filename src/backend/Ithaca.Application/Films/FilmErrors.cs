using Ithaca.Application.Common;
using Ithaca.Domain.Errors;

namespace Ithaca.Application.Films;

/// <summary>The film feature's errors, defined once.</summary>
public static class FilmErrors
{
    public static readonly Error NotFound = Error.NotFound(ErrorCodes.FilmNotFound, "This film isn't in our catalog.");
}
