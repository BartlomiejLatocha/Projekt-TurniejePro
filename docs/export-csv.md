# Eksport wyników do CSV (KAN-310)

Raport zawiera terminarz w kolejności rund i meczów oraz tabelę w kolejności pokazywanej w aplikacji. Format grupowo-pucharowy zawiera mecze fazy grupowej i pucharowej, a kolumna „Grupa” pozwala powiązać mecze grupowe z grupami w tabeli. Czysty puchar zawiera sam terminarz, tak jak widok turnieju. Przy braku wygenerowanego terminarza sekcja zawiera same nagłówki i nadal można odczytać dostępną tabelę.

## Format pliku

- Jeden plik z wierszem nazwy turnieju i wierszami „Terminarz” oraz „Tabela” rozpoczynającymi sekcje. Każdy wiersz ma 10 kolumn. Nieużywane pola pozostają puste.
- Kodowanie UTF-8 z BOM, separator średnik i zakończenia wierszy CRLF. Pola zawierające separator, cudzysłów lub znak końca wiersza są cytowane. Cudzysłowy wewnątrz pola są podwajane.
- Apostrof przed nazwą rozpoczynającą się jak formuła arkusza chroni przed jej wykonaniem. Brak wyniku jest zapisywany jako `-`, a wolny los jest oznaczony w kolumnie „Gość”.

## Integracja z pobieraniem pliku

`TournamentExportQuery.GetAsync(tournamentId, cancellationToken)` jest zarejestrowane w DI. Zwraca dane oparte na mapowaniu z endpointów `/schedule` i `/standings`. Dla nieistniejącego turnieju zgłasza domenowy `NotFoundException` obsługiwany przez globalny handler 404.

`TournamentCsvExport.Generate(data)` zwraca bajty pliku gotowe do zwrócenia przez endpoint KAN-311 z typem `TournamentCsvExport.ContentType` i rozszerzeniem `.csv`.

## Pobieranie pliku (KAN-311)

`GET /api/v1/tournaments/{tournamentId}/export/{format}`, gdzie `format` to `csv` albo `pdf` (wielkość liter nie ma znaczenia). Endpoint jest publiczny, tak jak terminarz i tabela.

| Sytuacja | Odpowiedź |
|---|---|
| Turniej istnieje | `200` z plikiem i nagłówkiem `Content-Disposition: attachment` |
| Format inny niż csv/pdf | `400` „Nieobsługiwany format” |
| Turniej nie istnieje | `404` (globalny handler, `NotFoundException`) |

Nazwa pliku powstaje z nazwy turnieju i daty pobrania, np. `liga-osiedlowa-bemowo-2026-11-23.csv`. Zawiera tylko małe litery ASCII, cyfry i myślniki (polskie znaki bez ogonków), więc działa w każdym systemie plików (`ExportFileName`).

Dla UI (KAN-314) wystarczy zwykły link do tego adresu: przeglądarka pobierze plik pod podaną nazwą.

