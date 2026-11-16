namespace TurniejePro.Contracts.Tournaments;

/// <summary>
/// Dyscyplina z listy wyboru wraz z domyślną punktacją. Kod jest stały i zapisywany w turnieju,
/// nazwa służy tylko do wyświetlania.
/// </summary>
/// <param name="Code">Kod dyscypliny, np. <c>football</c>.</param>
/// <param name="Name">Nazwa wyświetlana.</param>
/// <param name="PointsForWin">Domyślne punkty za wygraną.</param>
/// <param name="PointsForDraw">Domyślne punkty za remis (bez znaczenia, gdy remisy są niedozwolone).</param>
/// <param name="PointsForLoss">Domyślne punkty za porażkę.</param>
/// <param name="AllowDraws">Czy dyscyplina dopuszcza remisy.</param>
public sealed record DisciplineDefinition(
    string Code,
    string Name,
    int PointsForWin,
    int PointsForDraw,
    int PointsForLoss,
    bool AllowDraws);

/// <summary>
/// Stała lista dyscyplin obsługiwanych przez aplikację.
/// </summary>
public static class DisciplineCatalog
{
    public static readonly IReadOnlyList<DisciplineDefinition> All =
    [
        new("football", "Piłka nożna", 3, 1, 0, true),
        new("basketball", "Koszykówka", 2, 0, 1, false),
        new("volleyball", "Siatkówka", 3, 0, 0, false),
        new("chess", "Szachy", 2, 1, 0, true),
        new("esports", "E-sport", 3, 1, 0, true)
    ];

    /// <summary>
    /// Znajduje dyscyplinę po kodzie albo (dla starych danych wpisanych jako tekst) po nazwie,
    /// bez względu na wielkość liter i polskie znaki, np. „Pilka nozna” → piłka nożna.
    /// </summary>
    public static DisciplineDefinition? Find(string? codeOrName)
    {
        if (string.IsNullOrWhiteSpace(codeOrName))
        {
            return null;
        }

        var key = Simplify(codeOrName);
        return All.FirstOrDefault(d => Simplify(d.Code) == key || Simplify(d.Name) == key);
    }

    private static string Simplify(string text)
    {
        var decomposed = text.Trim().Normalize(System.Text.NormalizationForm.FormD);
        var chars = decomposed
            .Where(c => System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c) != System.Globalization.UnicodeCategory.NonSpacingMark);
        // „ł” nie rozkłada się na literę i znak diakrytyczny, więc zamieniamy je osobno.
        return new string(chars.ToArray()).Replace('ł', 'l').Replace('Ł', 'L').ToLowerInvariant();
    }

    /// <summary>
    /// Nazwa dyscypliny do wyświetlenia. Dla wartości spoza katalogu (stare dane) zwraca wartość bez zmian.
    /// </summary>
    public static string NameOf(string? code) => Find(code)?.Name ?? code ?? string.Empty;
}
