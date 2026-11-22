# Wybór biblioteki PDF (KAN-312)

Raport ma zawierać terminarz i tabelę na stronach A4. Kryteria to polskie znaki niezależnie od fontów systemowych, zawijanie długich nazw, powtarzanie nagłówków tabel przy zmianie strony oraz generowanie w .NET 10 na Windows i Linux.

| Biblioteka | Licencja | Układ i wdrożenie |
| --- | --- | --- |
| QuestPDF 2026.9.2 | Community v3.0 dla kwalifikujących się użytkowników, w tym osób uczących się. Dostępny kod źródłowy. | Paginacja i powtarzalne nagłówki tabel, dołączone Lato (SIL OFL 1.1), biblioteki natywne w pakiecie NuGet. |
| PDFsharp + MigraDoc 6.5.0 | AGPL lub licencja komercyjna. Wersja 6.2.4 była dostępna na MIT i zakończyła cykl aktualizacji. | MigraDoc automatycznie dzieli tabele na strony. W wariancie Core trzeba zapewnić źródło fontów przewidywalne na obu systemach. |

## Decyzja

Stosujemy **QuestPDF 2026.9.2** w serwerowym generatorze KAN-313. Przyjmujemy zastosowanie edukacyjne w projekcie grupowym. Przy innym zastosowaniu kwalifikację do wariantu Community należy ocenić według warunków licencji.

## Wynik próby

Generator KAN-313 na Windows i .NET 10 utworzył trzy strony A4 dla przykładu z 66 meczami. Render Popplera potwierdził polskie znaki, zawijanie długiej nazwy drużyny w terminarzu i tabeli, powtarzanie nagłówka oraz numerację stron. Odczyt PdfPig potwierdził tekstową warstwę raportu. Zgodność z Linuxem deklaruje producent biblioteki.

## Źródła

- [Licencja QuestPDF Community i kryteria wyboru](https://www.questpdf.com/license/guide.html)
- [QuestPDF 2026.9.2 w NuGet](https://www.nuget.org/packages/QuestPDF/2026.9.2)
- [Fonty QuestPDF](https://www.questpdf.com/api-reference/text/font-management.html)
- [Powtarzanie nagłówków tabel w QuestPDF](https://www.questpdf.com/api-reference/table/header-and-footer.html)
- [Licencje PDFsharp i MigraDoc](https://docs.pdfsharp.net/LICENSE.html)
- [Stan wsparcia wersji PDFsharp i MigraDoc](https://www.pdfsharp.com/)
- [Obsługa fontów w PDFsharp Core](https://docs.pdfsharp.net/PDFsharp/Topics/Fonts/Font-Resolving.html)
