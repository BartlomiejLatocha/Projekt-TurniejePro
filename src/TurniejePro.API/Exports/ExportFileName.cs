using System.Globalization;
using System.Text;

namespace TurniejePro.Api.Exports;

/// <summary>
/// Nazwa pobieranego pliku z raportem, np. <c>liga-osiedlowa-bemowo-2026-11-23.csv</c>. Tylko małe litery ASCII, cyfry
/// i myślniki, więc działa w każdym systemie plików i przeglądarce, bez znaków zakazanych w Windows (<c>: / \ ? *</c>).
/// </summary>
public static class ExportFileName
{
    private const int MaxSlugLength = 60;

    public static string For(string tournamentName, DateOnly date, string extension)
    {
        ArgumentNullException.ThrowIfNull(tournamentName);
        ArgumentException.ThrowIfNullOrWhiteSpace(extension);

        var slug = Slug(tournamentName);
        var datePart = date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

        return $"{(slug.Length == 0 ? "turniej" : slug)}-{datePart}.{extension}";
    }

    private static string Slug(string text)
    {
        // „ł” nie rozkłada się na literę i znak diakrytyczny, więc zamieniamy ją ręcznie
        var normalized = text
            .Replace('ł', 'l')
            .Replace('Ł', 'L')
            .Normalize(NormalizationForm.FormD);

        var builder = new StringBuilder(normalized.Length);
        foreach (var character in normalized)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            if (char.IsAsciiLetterOrDigit(character))
            {
                builder.Append(char.ToLowerInvariant(character));
            }
            else if (builder.Length > 0 && builder[^1] != '-')
            {
                builder.Append('-');
            }
        }

        var slug = builder.ToString().Trim('-');
        return slug.Length <= MaxSlugLength ? slug : slug[..MaxSlugLength].TrimEnd('-');
    }
}
