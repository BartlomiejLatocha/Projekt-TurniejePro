namespace TurniejePro.Contracts.Common;

/// <summary>
/// Strona wyników listy. Wszystkie endpointy zwracające listy używają tej koperty.
/// </summary>
/// <typeparam name="T">Typ elementu listy.</typeparam>
/// <param name="Items">Elementy bieżącej strony.</param>
/// <param name="Page">Numer strony (od 1).</param>
/// <param name="PageSize">Rozmiar strony.</param>
/// <param name="TotalCount">Łączna liczba elementów we wszystkich stronach.</param>
public sealed record PagedResponse<T>(
    IReadOnlyList<T> Items,
    int Page,
    int PageSize,
    int TotalCount);
