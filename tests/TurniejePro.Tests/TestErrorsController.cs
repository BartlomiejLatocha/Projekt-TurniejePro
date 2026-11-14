using System.Diagnostics.CodeAnalysis;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TurniejePro.Domain.Exceptions;

namespace TurniejePro.Tests.Controllers;

/// <summary>
/// Endpointy testowe, które rzucają wybrane wyjątki, żeby sprawdzić globalną obsługę błędów (KAN-303).
/// </summary>
[SuppressMessage("Performance", "CA1822:Mark members as static", Justification = "Akcje kontrolera MVC nie mogą być statyczne.")]
[ApiController]
[Route("api/test-errors")]
[AllowAnonymous]
public class TestErrorsController : ControllerBase
{
    public const string UnexpectedMessage = "Szczegół wewnętrzny, którego klient nie powinien zobaczyć";

    [HttpGet("not-found")]
    public IActionResult NotFoundError() => throw new NotFoundException("Nie ma takiego meczu.");

    [HttpGet("conflict")]
    public IActionResult ConflictError() => throw new ConflictException("Terminarz już istnieje.");

    [HttpGet("business-rule")]
    public IActionResult BusinessRuleError() => throw new BusinessRuleException("W pucharze nie ma remisów.");

    [HttpGet("argument")]
    public IActionResult ArgumentError() => throw new ArgumentException("Nazwa nie może być pusta.", "name");

    [HttpGet("unexpected")]
    public IActionResult UnexpectedError() => throw new InvalidOperationException(UnexpectedMessage);
}
