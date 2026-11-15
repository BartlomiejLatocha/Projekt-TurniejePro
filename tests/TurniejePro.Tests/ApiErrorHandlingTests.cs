using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using TurniejePro.Api.Auth;
using TurniejePro.Api.Errors;
using TurniejePro.Contracts.Tournaments;
using TurniejePro.Data;
using TurniejePro.Domain;
using TurniejePro.Domain.Exceptions;
using TurniejePro.Tests.Controllers;
using DomainFormat = TurniejePro.Domain.Enums.TournamentFormat;

namespace TurniejePro.Tests;

/// <summary>
/// Globalna obsługa błędów (KAN-303): wyjątki domenowe → 400/404/409, inne → 500 bez szczegółów,
/// każda odpowiedź błędu jako ProblemDetails z polskim tytułem.
/// </summary>
public class ApiErrorHandlingTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;
    private readonly TestTokens _tokens;

    public ApiErrorHandlingTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
        _tokens = new TestTokens(factory.Services);
    }

    // --- Wyjątki rzucane w kontrolerze ---

    [Theory]
    [InlineData("not-found", HttpStatusCode.NotFound, ProblemTitles.NotFound, "Nie ma takiego meczu.")]
    [InlineData("conflict", HttpStatusCode.Conflict, ProblemTitles.Conflict, "Terminarz już istnieje.")]
    [InlineData("business-rule", HttpStatusCode.BadRequest, ProblemTitles.BusinessRule, "W pucharze nie ma remisów.")]
    [InlineData("argument", HttpStatusCode.BadRequest, ProblemTitles.InvalidData, "Nazwa nie może być pusta.")]
    public async Task DomainException_BecomesProblemDetailsWithPolishTitleAndMessage(
        string path, HttpStatusCode expectedStatus, string expectedTitle, string expectedDetail)
    {
        using var response = await _client.GetAsync($"/api/test-errors/{path}");

        var problem = await ReadProblemAsync(response);
        Assert.Equal(expectedStatus, response.StatusCode);
        Assert.Equal((int)expectedStatus, problem.Status);
        Assert.Equal(expectedTitle, problem.Title);
        Assert.Equal(expectedDetail, problem.Detail);
    }

    [Fact]
    public async Task UnexpectedException_Returns500WithoutInternalDetails()
    {
        using var response = await _client.GetAsync("/api/test-errors/unexpected");

        var body = await response.Content.ReadAsStringAsync();
        var problem = await ReadProblemAsync(response);
        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Equal(ProblemTitles.ServerError, problem.Title);
        Assert.DoesNotContain(TestErrorsController.UnexpectedMessage, body, StringComparison.Ordinal);
    }

    // --- Prawdziwe endpointy ---

    [Fact]
    public async Task GenerateSchedule_WithTooFewTeams_Returns400WithDomainMessage()
    {
        var tournamentId = await SeedTournamentAsync();

        using var response = await SendAsync(HttpMethod.Post, $"/api/v1/tournaments/{tournamentId}/schedule", _tokens.ForRoles(Roles.Admin));

        var problem = await ReadProblemAsync(response);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(ProblemTitles.BusinessRule, problem.Title);
        Assert.Contains("co najmniej 2", problem.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CreateTournament_WithInvalidBody_ReturnsPolishValidationTitle()
    {
        var invalid = new CreateTournamentRequest("AB", "Szachy", TournamentFormat.Cup, DateTimeOffset.UtcNow.AddDays(1), MaxTeams: 1);

        using var response = await SendAsync(HttpMethod.Post, "/api/v1/tournaments", _tokens.ForRoles(Roles.User), invalid);

        var problem = await ReadProblemAsync(response);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(ProblemTitles.Validation, problem.Title);
    }

    [Fact]
    public async Task MissingToken_Returns401AsProblemDetails()
    {
        using var response = await _client.PostAsync("/api/v1/tournaments", null);

        var problem = await ReadProblemAsync(response);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal(ProblemTitles.Unauthorized, problem.Title);
    }

    [Fact]
    public async Task ForbiddenResource_Returns403AsProblemDetails()
    {
        var tournamentId = await SeedTournamentAsync();

        using var response = await SendAsync(HttpMethod.Delete, $"/api/v1/tournaments/{tournamentId}", _tokens.ForRoles(Roles.User));

        var problem = await ReadProblemAsync(response);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(ProblemTitles.Forbidden, problem.Title);
    }

    [Fact]
    public async Task TitleSetInController_IsKept()
    {
        using var response = await _client.GetAsync($"/api/v1/tournaments/{Guid.NewGuid()}/schedule");

        var problem = await ReadProblemAsync(response);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("Nie znaleziono turnieju", problem.Title);
    }

    // --- Jednostkowo ---

    [Fact]
    public void Map_UnknownException_ReturnsNull()
    {
        Assert.Null(DomainExceptionHandler.Map(new InvalidOperationException("błąd w kodzie")));
    }

    [Fact]
    public void Map_DerivedConflictExceptions_Return409()
    {
        Assert.Equal(StatusCodes.Status409Conflict, DomainExceptionHandler.Map(new Domain.Scheduling.ScheduleAlreadyExistsException(Guid.NewGuid()))?.Status);
        Assert.Equal(StatusCodes.Status409Conflict, DomainExceptionHandler.Map(new Domain.Scheduling.MatchResultConflictException("x"))?.Status);
    }

    [Fact]
    public void UserMessage_RemovesTechnicalSuffixFromArgumentExceptions()
    {
        Assert.Equal("Nazwa nie może być pusta.", DomainExceptionHandler.UserMessage(new ArgumentException("Nazwa nie może być pusta.", "name")));
        Assert.Equal("Limit poza zakresem.", DomainExceptionHandler.UserMessage(new ArgumentOutOfRangeException("maxTeams", 1, "Limit poza zakresem.")));
        Assert.Equal("Zwykły komunikat.", DomainExceptionHandler.UserMessage(new BusinessRuleException("Zwykły komunikat.")));
    }

    [Theory]
    [InlineData(null, 404, ProblemTitles.NotFound)]
    [InlineData("Not Found", 404, ProblemTitles.NotFound)]
    [InlineData("Bad Request", 400, ProblemTitles.BadRequest)]
    [InlineData("Forbidden", 403, ProblemTitles.Forbidden)]
    [InlineData("Unauthorized", 401, ProblemTitles.Unauthorized)]
    [InlineData("Conflict", 409, ProblemTitles.Conflict)]
    [InlineData("An error occurred while processing your request.", 500, ProblemTitles.ServerError)]
    [InlineData("Not Acceptable", 406, ProblemTitles.Error)]
    [InlineData("Nazwa drużyny zajęta", 400, "Nazwa drużyny zajęta")]
    public void ApplyPolishTitle_ReplacesOnlyFrameworkTitles(string? title, int status, string expected)
    {
        var problem = new ProblemDetails { Title = title, Status = status };

        ProblemTitles.ApplyPolishTitle(problem);

        Assert.Equal(expected, problem.Title);
    }

    [Fact]
    public void ApplyPolishTitle_ValidationProblem_GetsValidationTitle()
    {
        var problem = new ValidationProblemDetails { Title = "One or more validation errors occurred.", Status = 400 };

        ProblemTitles.ApplyPolishTitle(problem);

        Assert.Equal(ProblemTitles.Validation, problem.Title);
    }

    [Fact]
    public void DomainExceptions_KeepMessageAndInnerException()
    {
        var inner = new InvalidOperationException("przyczyna");
        DomainException[] withInner =
        [
            new NotFoundException("a", inner),
            new ConflictException("b", inner),
            new BusinessRuleException("c", inner)
        ];
        DomainException[] withoutMessage = [new NotFoundException(), new ConflictException(), new BusinessRuleException()];

        Assert.All(withInner, exception => Assert.Same(inner, exception.InnerException));
        Assert.Equal(["a", "b", "c"], withInner.Select(exception => exception.Message));
        Assert.All(withoutMessage, exception => Assert.NotNull(DomainExceptionHandler.Map(exception)));
    }

    [Fact]
    public void ApplyPolishTitle_Null_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => ProblemTitles.ApplyPolishTitle(null!));
    }

    [Fact]
    public async Task Handler_NullArguments_Throw()
    {
        using var scope = _factory.Services.CreateScope();
        var handler = new DomainExceptionHandler(scope.ServiceProvider.GetRequiredService<IProblemDetailsService>());

        await Assert.ThrowsAsync<ArgumentNullException>(async () => await handler.TryHandleAsync(null!, new NotFoundException(), default));
        await Assert.ThrowsAsync<ArgumentNullException>(async () => await handler.TryHandleAsync(new DefaultHttpContext(), null!, default));
        Assert.Throws<ArgumentNullException>(() => DomainExceptionHandler.UserMessage(null!));
    }

    // --- Pomocnicze ---

    private static async Task<ProblemDetails> ReadProblemAsync(HttpResponseMessage response)
    {
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.NotNull(problem);
        return problem;
    }

    private async Task<HttpResponseMessage> SendAsync(HttpMethod method, string url, string token, object? body = null)
    {
        using var request = new HttpRequestMessage(method, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        if (body is not null)
        {
            request.Content = JsonContent.Create(body, body.GetType());
        }

        return await _client.SendAsync(request);
    }

    private async Task<Guid> SeedTournamentAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TurniejeProDbContext>();

        var tournament = new Tournament($"Turniej {Guid.NewGuid()}", "Piłka nożna", DomainFormat.League, DateTimeOffset.UtcNow.AddDays(7), 8, Guid.NewGuid());
        db.Tournaments.Add(tournament);
        await db.SaveChangesAsync();

        return tournament.Id;
    }
}
