using System.ComponentModel.DataAnnotations;
using Ithaca.Domain.Library;

namespace Ithaca.Application.Library;

/// <summary>
/// Request validation for a rating: null, or 0.5–5 in steps of 0.5 (<see cref="UserFilm.IsValidRating"/>, ADR-0024).
/// Anything else is 400 <c>validation</c> with <c>errors.rating</c>.
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Parameter)]
public sealed class HalfStarRatingAttribute : ValidationAttribute
{
    public HalfStarRatingAttribute()
        : base("The rating must be 0.5-5 in steps of 0.5.")
    {
    }

    public override bool IsValid(object? value) => value switch
    {
        null => true,
        decimal rating => UserFilm.IsValidRating(rating),
        _ => false,
    };
}
