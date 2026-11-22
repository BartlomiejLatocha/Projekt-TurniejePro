# Eksport wyników do CSV (KAN-310)

Raport zawiera terminarz w kolejności rund i meczów oraz tabelę w kolejności pokazywanej w aplikacji. Format grupowo-pucharowy zawiera mecze fazy grupowej i pucharowej, a kolumna „Grupa” pozwala powiązać mecze grupowe z grupami w tabeli. Czysty puchar zawiera sam terminarz, tak jak widok turnieju. Przy braku wygenerowanego terminarza sekcja zawiera same nagłówki i nadal można odczytać dostępną tabelę.

## Format pliku

- Jeden plik z wierszem nazwy turnieju i wierszami „Terminarz” oraz „Tabela” rozpoczynającymi sekcje. Każdy wiersz ma 10 kolumn. Nieużywane pola pozostają puste.
- Kodowanie UTF-8 z BOM, separator średnik i zakończenia wierszy CRLF. Pola zawierające separator, cudzysłów lub znak końca wiersza są cytowane. Cudzysłowy wewnątrz pola są podwajane.
- Apostrof przed nazwą rozpoczynającą się jak formuła arkusza chroni przed jej wykonaniem. Brak wyniku jest zapisywany jako `-`, a wolny los jest oznaczony w kolumnie „Gość”.

## Integracja z pobieraniem pliku

`TournamentExportQuery.GetAsync(tournamentId, cancellationToken)` jest zarejestrowane w DI. Zwraca dane oparte na mapowaniu z endpointów `/schedule` i `/standings`. Dla nieistniejącego turnieju zgłasza domenowy `NotFoundException` obsługiwany przez globalny handler 404.

`TournamentCsvExport.Generate(data)` zwraca bajty pliku gotowe do zwrócenia przez endpoint KAN-311 z typem `TournamentCsvExport.ContentType` i rozszerzeniem `.csv`.
