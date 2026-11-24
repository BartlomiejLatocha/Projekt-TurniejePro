using System.Globalization;
using System.Text;

namespace TurniejePro.Domain;

/// <summary>
/// Porównywanie tekstu w wyszukiwarce: bez wielkości liter i bez polskich znaków, więc „lodz” znajdzie „Łódź”,
/// a „żubry” znajdzie „Żubry Mokotów”. SQLite porównuje bez wielkości liter tylko litery angielskie, dlatego
/// tekst porównujemy w aplikacji, a nie w zapytaniu SQL.
/// </summary>
public static class SearchText
{
    /// <summary>
    /// Tekst bez znaków diakrytycznych, małymi literami, z pojedynczymi spacjami.
    /// </summary>
    public static string Normalize(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        // „ł” nie rozkłada się na literę i znak diakrytyczny, więc zamieniamy ją ręcznie
        var decomposed = text.Replace('ł', 'l').Replace('Ł', 'L').Normalize(NormalizationForm.FormD);

        var builder = new StringBuilder(decomposed.Length);
        foreach (var character in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            if (char.IsWhiteSpace(character))
            {
                if (builder.Length > 0 && builder[^1] != ' ')
                {
                    builder.Append(' ');
                }

                continue;
            }

            builder.Append(char.ToLowerInvariant(character));
        }

        return builder.ToString().TrimEnd();
    }

    /// <summary>
    /// Czy <paramref name="text"/> zawiera <paramref name="query"/> (po normalizacji obu). Pusta fraza pasuje do wszystkiego.
    /// </summary>
    public static bool Contains(string? text, string? query)
    {
        var normalizedQuery = Normalize(query);
        return normalizedQuery.Length == 0 || Normalize(text).Contains(normalizedQuery, StringComparison.Ordinal);
    }
}
