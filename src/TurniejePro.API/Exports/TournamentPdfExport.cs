using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace TurniejePro.Api.Exports;

/// <summary>Generuje raport PDF z terminarzem i tabelą turnieju.</summary>
public static class TournamentPdfExport
{
    public const string ContentType = "application/pdf";

    static TournamentPdfExport()
    {
        QuestPDF.Settings.License ??= LicenseType.Community;
    }

    public static byte[] Generate(TournamentExportData data)
    {
        ArgumentNullException.ThrowIfNull(data);

        return Document.Create(document => document.Page(page =>
        {
            page.Size(PageSizes.A4);
            page.Margin(32);
            page.DefaultTextStyle(style => style.FontFamily("Lato").FontSize(9).FontColor(Colors.BlueGrey.Darken4));

            page.Header().PaddingBottom(16).Column(header =>
            {
                header.Item().Text("TurniejePro").SemiBold().FontColor(Colors.BlueGrey.Darken2);
                header.Item().PaddingTop(4).Text(data.TournamentName).FontSize(20).Bold();
            });

            page.Content().Column(content =>
            {
                content.Spacing(18);
                WriteSection(content.Item(), "Terminarz", "Brak wygenerowanego terminarza.",
                    TournamentExportTables.ScheduleHeaders, [11, 5, 7, 23, 10, 23, 16],
                    TournamentExportTables.ScheduleRows(data.Schedule));

                if (data.Standings is { } standings)
                {
                    WriteSection(content.Item(), "Tabela", "Brak zaakceptowanych drużyn w tabeli.",
                        TournamentExportTables.StandingsHeaders, [7, 9, 31, 5, 5, 5, 5, 12, 10, 6],
                        TournamentExportTables.StandingsRows(standings));
                }
            });

            page.Footer().PaddingTop(12).AlignRight().Text(footer =>
            {
                footer.Span("Strona ");
                footer.CurrentPageNumber();
                footer.Span(" / ");
                footer.TotalPages();
            });
        })).GeneratePdf();
    }

    private static void WriteSection(
        IContainer container,
        string title,
        string emptyMessage,
        IReadOnlyList<string> headers,
        IReadOnlyList<float> columnWidths,
        IEnumerable<string[]> rows)
    {
        var tableRows = rows.ToArray();
        container.EnsureSpace(76).Column(section =>
        {
            section.Spacing(6);
            section.Item().Text(title).FontSize(13).Bold();
            if (tableRows.Length == 0)
            {
                section.Item().Text(emptyMessage);
                return;
            }

            section.Item().Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    foreach (var width in columnWidths)
                    {
                        columns.RelativeColumn(width);
                    }
                });

                table.Header(header =>
                {
                    foreach (var label in headers)
                    {
                        header.Cell().Background(Colors.BlueGrey.Lighten4).Padding(5).Text(label).SemiBold();
                    }
                });

                foreach (var row in tableRows)
                {
                    foreach (var value in row)
                    {
                        table.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(5).Text(value);
                    }
                }
            });
        });
    }
}
