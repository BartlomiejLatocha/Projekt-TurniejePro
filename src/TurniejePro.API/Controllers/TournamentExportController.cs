using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TurniejePro.Api.Exports;

namespace TurniejePro.Api.Controllers;

/// <summary>
/// Pobieranie raportu turnieju (terminarz i tabela) jako pliku CSV lub PDF (KAN-311). Dane i wygląd plików
/// przygotowują <see cref="TournamentExportQuery"/>, <see cref="TournamentCsvExport"/> i <see cref="TournamentPdfExport"/>.
/// </summary>
[ApiController]
[Route("api/v1/tournaments/{tournamentId:guid}/export")]
public class TournamentExportController : ControllerBase
{
    private readonly TournamentExportQuery _exportQuery;
    private readonly TimeProvider _timeProvider;

    public TournamentExportController(TournamentExportQuery exportQuery, TimeProvider timeProvider)
    {
        _exportQuery = exportQuery;
        _timeProvider = timeProvider;
    }

    /// <summary>
    /// Zwraca plik z terminarzem i tabelą turnieju. Format: <c>csv</c> (UTF-8, średnik, do arkusza) albo <c>pdf</c>.
    /// Publiczny, tak jak terminarz i tabela. Nazwa pliku powstaje z nazwy turnieju i daty pobrania.
    /// </summary>
    /// <param name="tournamentId">Identyfikator turnieju.</param>
    /// <param name="format">Format pliku: csv albo pdf (wielkość liter nie ma znaczenia).</param>
    /// <param name="cancellationToken">Token anulowania.</param>
    [HttpGet("{format}")]
    [AllowAnonymous]
    [Produces("text/csv", "application/pdf", "application/problem+json")]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Download(Guid tournamentId, string format, CancellationToken cancellationToken)
    {
        var normalizedFormat = format.ToLowerInvariant();
        if (normalizedFormat is not ("csv" or "pdf"))
        {
            return Problem(
                detail: $"Nieobsługiwany format pliku „{format}”. Dostępne formaty: csv, pdf.",
                statusCode: StatusCodes.Status400BadRequest,
                title: "Nieobsługiwany format");
        }

        // Nieistniejący turniej: NotFoundException z TournamentExportQuery → 404 z globalnego handlera
        var data = await _exportQuery.GetAsync(tournamentId, cancellationToken);
        var fileName = ExportFileName.For(data.TournamentName, DateOnly.FromDateTime(_timeProvider.GetUtcNow().UtcDateTime), normalizedFormat);

        return normalizedFormat == "csv"
            ? File(TournamentCsvExport.Generate(data), TournamentCsvExport.ContentType, fileName)
            : File(TournamentPdfExport.Generate(data), TournamentPdfExport.ContentType, fileName);
    }
}
