using System.Globalization;
using System.Text;

namespace Ithaca.Application.Imports;

/// <summary>
/// The one title comparison of the import (§13 job rule 2): ignores case, accents and punctuation. "Amélie" = "Amelie",
/// "Spider-Man: No Way Home" = "Spider Man No Way Home", "Schindler's List" = "Schindlers List".
/// </summary>
public static class TitleNormalizer
{
    public static string Normalize(string title)
    {
        ArgumentNullException.ThrowIfNull(title);
        var builder = new StringBuilder(title.Length);
        var pendingSpace = false;
        foreach (var c in title.Normalize(NormalizationForm.FormD))
        {
            var category = CharUnicodeInfo.GetUnicodeCategory(c);
            if (category is UnicodeCategory.NonSpacingMark or UnicodeCategory.SpacingCombiningMark or UnicodeCategory.EnclosingMark
                || c is '\'' or '’' or '‘')
            {
                continue; // accents and apostrophes vanish
            }

            if (char.IsLetterOrDigit(c))
            {
                if (pendingSpace && builder.Length > 0)
                {
                    builder.Append(' ');
                }

                builder.Append(char.ToLowerInvariant(c));
                pendingSpace = false;
            }
            else
            {
                pendingSpace = true; // whitespace, punctuation and symbols separate words
            }
        }

        return builder.ToString();
    }
}
