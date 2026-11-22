using System.Text;

namespace TurniejePro.Api.Exports;

/// <summary>Generuje jednoplikowy raport CSV z terminarzem i tabelą turnieju.</summary>
public static class TournamentCsvExport
{
    public const string ContentType = "text/csv; charset=utf-8";

    public static byte[] Generate(TournamentExportData data)
    {
        ArgumentNullException.ThrowIfNull(data);

        using var output = new MemoryStream();
        using (var writer = new StreamWriter(output, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true), leaveOpen: true))
        {
            writer.NewLine = "\r\n";
            WriteRow(writer, ["Turniej", ProtectSpreadsheetText(data.TournamentName)]);
            WriteRow(writer, ["Terminarz"]);
            WriteRow(writer, TournamentExportTables.ScheduleHeaders);
            foreach (var row in TournamentExportTables.ScheduleRows(data.Schedule))
            {
                row[3] = ProtectSpreadsheetText(row[3]);
                row[5] = ProtectSpreadsheetText(row[5]);
                WriteRow(writer, row);
            }

            if (data.Standings is { } standings)
            {
                WriteRow(writer, ["Tabela"]);
                WriteRow(writer, TournamentExportTables.StandingsHeaders);
                foreach (var row in TournamentExportTables.StandingsRows(standings))
                {
                    row[2] = ProtectSpreadsheetText(row[2]);
                    WriteRow(writer, row);
                }
            }
        }

        return output.ToArray();
    }

    private static void WriteRow(TextWriter writer, IReadOnlyList<string> values)
    {
        writer.Write(string.Join(';', values.Select(Escape)));
        for (var index = values.Count; index < TournamentExportTables.StandingsHeaders.Count; index++)
        {
            writer.Write(';');
        }

        writer.WriteLine();
    }

    private static string Escape(string value) =>
        value.AsSpan().ContainsAny(';', '"') || value.AsSpan().ContainsAny('\r', '\n')
            ? $"\"{value.Replace("\"", "\"\"", StringComparison.Ordinal)}\""
            : value;

    private static string ProtectSpreadsheetText(string value)
    {
        var trimmed = value.AsSpan().TrimStart();
        return (value.Length > 0 && value[0] is '\t' or '\r' or '\n')
            || (trimmed.Length > 0 && trimmed[0] is '=' or '+' or '-' or '@')
            ? $"'{value}"
            : value;
    }
}
