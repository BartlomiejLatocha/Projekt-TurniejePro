# Eksport wyników do PDF (KAN-313)

Serwer generuje PDF w formacie A4 zawierający nazwę turnieju, terminarz i tabelę w kolejności zgodnej z aplikacją. W formacie grupowo-pucharowym kolumna „Grupa” łączy mecze fazy grupowej z tabelą grupy. Czysty puchar zawiera terminarz, a brak meczów lub wierszy tabeli ma odrębny komunikat. Nagłówki kolumn powtarzają się na kolejnych stronach. Tekst pozostaje zaznaczalny.

PDF i CSV korzystają z tych samych danych. Generator PDF używa dołączonego do QuestPDF fontu Lato, który zawiera polskie znaki, oraz licencji Community przyjętej w KAN-312 dla zastosowania edukacyjnego.

## Integracja z pobieraniem pliku

Wynik `TournamentExportQuery.GetAsync(tournamentId, cancellationToken)` przekaż do `TournamentPdfExport.Generate(data)`. Wynik generatora to bajty pliku o typie `TournamentPdfExport.ContentType` i rozszerzeniu `.pdf`. Sposób pobrania danych i zachowanie dla nieistniejącego turnieju są wspólne z [CSV](export-csv.md).
