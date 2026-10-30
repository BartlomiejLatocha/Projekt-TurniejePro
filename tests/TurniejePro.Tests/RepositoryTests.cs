using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using TurniejePro.Data;
using TurniejePro.Data.Repositories;
using TurniejePro.Domain;
using TurniejePro.Domain.Enums;
using Xunit;

namespace TurniejePro.Tests;

public class RepositoryTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<TurniejeProDbContext> _options;

    public RepositoryTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        _options = new DbContextOptionsBuilder<TurniejeProDbContext>()
            .UseSqlite(_connection)
            .Options;

        using var context = new TurniejeProDbContext(_options);
        context.Database.EnsureCreated();
    }

    public void Dispose()
    {
        _connection.Dispose();
        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task GenericRepository_AddAndGetById_ShouldPersistEntity()
    {
        await using var context = new TurniejeProDbContext(_options);
        var repository = new Repository<Tournament>(context);
        var tournament = new Tournament("Turniej Testowy");

        await repository.AddAsync(tournament);
        await context.SaveChangesAsync();

        var retrieved = await repository.GetByIdAsync(tournament.Id);

        Assert.NotNull(retrieved);
        Assert.Equal("Turniej Testowy", retrieved.Name);
    }

    [Fact]
    public async Task GenericRepository_GetAll_ShouldReturnAllEntities()
    {
        await using var context = new TurniejeProDbContext(_options);
        var repository = new Repository<Tournament>(context);
        await repository.AddAsync(new Tournament("T1"));
        await repository.AddAsync(new Tournament("T2"));
        await context.SaveChangesAsync();

        var all = await repository.GetAllAsync();

        Assert.Equal(2, all.Count);
    }

    [Fact]
    public async Task GenericRepository_Update_ShouldModifyEntity()
    {
        await using var context = new TurniejeProDbContext(_options);
        var repository = new Repository<Tournament>(context);
        var tournament = new Tournament("Przed Zmianą");
        await repository.AddAsync(tournament);
        await context.SaveChangesAsync();

        tournament.Name = "Po Zmianie";
        repository.Update(tournament);
        await context.SaveChangesAsync();

        var updated = await repository.GetByIdAsync(tournament.Id);

        Assert.NotNull(updated);
        Assert.Equal("Po Zmianie", updated.Name);
    }

    [Fact]
    public async Task GenericRepository_Delete_ShouldRemoveEntity()
    {
        await using var context = new TurniejeProDbContext(_options);
        var repository = new Repository<Tournament>(context);
        var tournament = new Tournament("Do Usunięcia");
        await repository.AddAsync(tournament);
        await context.SaveChangesAsync();

        repository.Delete(tournament);
        await context.SaveChangesAsync();

        var deleted = await repository.GetByIdAsync(tournament.Id);

        Assert.Null(deleted);
    }

    [Fact]
    public async Task TournamentRepository_GetWithDetailsAsync_ShouldIncludeRelatedData()
    {
        await using var context = new TurniejeProDbContext(_options);
        var repository = new TournamentRepository(context);

        var tournament = new Tournament("Wielki Turniej", "Siatkówka", TournamentFormat.League, DateTimeOffset.UtcNow, 8);
        var team = new Team("Sokoły", tournament.Id);
        team.AddPlayer("Jan Nowak", 5);
        var match = new Match(tournament.Id, 1, team.Id, null);
        var token = new CollectibleToken("SOKOL", "Sokół", tournament.Id);

        context.Tournaments.Add(tournament);
        context.Teams.Add(team);
        context.Matches.Add(match);
        context.CollectibleTokens.Add(token);
        await context.SaveChangesAsync();

        var result = await repository.GetWithDetailsAsync(tournament.Id);

        Assert.NotNull(result);
        Assert.Single(result.Teams);
        Assert.Single(result.Teams.First().Players);
        Assert.Single(result.Matches);
        Assert.Single(result.Tokens);
    }

    [Fact]
    public async Task TeamRepository_GetByTournamentIdAsync_ShouldReturnTeamsForSpecificTournament()
    {
        await using var context = new TurniejeProDbContext(_options);
        var repository = new TeamRepository(context);

        var tournament1 = new Tournament("Turniej 1");
        var tournament2 = new Tournament("Turniej 2");

        var team1 = new Team("Team T1", tournament1.Id);
        team1.AddPlayer("Gracz 1", 1);
        var team2 = new Team("Team T2", tournament2.Id);

        context.Tournaments.AddRange(tournament1, tournament2);
        context.Teams.AddRange(team1, team2);
        await context.SaveChangesAsync();

        var teamsForT1 = await repository.GetByTournamentIdAsync(tournament1.Id);

        Assert.Single(teamsForT1);
        Assert.Equal("Team T1", teamsForT1[0].Name);
        Assert.Single(teamsForT1[0].Players);
    }

    [Fact]
    public async Task TeamRepository_GetWithPlayersAsync_ShouldIncludePlayers()
    {
        await using var context = new TurniejeProDbContext(_options);
        var repository = new TeamRepository(context);

        var tournament = new Tournament("Turniej");
        var team = new Team("Orły", tournament.Id);
        team.AddPlayer("Kamil Stoch", 1);
        team.AddPlayer("Piotr Żyła", 2);

        context.Tournaments.Add(tournament);
        context.Teams.Add(team);
        await context.SaveChangesAsync();

        var result = await repository.GetWithPlayersAsync(team.Id);

        Assert.NotNull(result);
        Assert.Equal("Orły", result.Name);
        Assert.Equal(2, result.Players.Count);
    }

    [Fact]
    public async Task TeamRepository_GetByCaptainIdAsync_ShouldReturnTeamsForCaptain()
    {
        await using var context = new TurniejeProDbContext(_options);
        var repository = new TeamRepository(context);
        var captainId = Guid.NewGuid();

        var tournament = new Tournament("Turniej");
        var team1 = new Team("Team 1", tournament.Id, captainId);
        var team2 = new Team("Team 2", tournament.Id, captainId);
        var teamOther = new Team("Team Other", tournament.Id, Guid.NewGuid());

        context.Tournaments.Add(tournament);
        context.Teams.AddRange(team1, team2, teamOther);
        await context.SaveChangesAsync();

        var results = await repository.GetByCaptainIdAsync(captainId);

        Assert.Equal(2, results.Count);
        Assert.All(results, t => Assert.Equal(captainId, t.CaptainId));
    }

    [Fact]
    public async Task TeamRepository_GetByStatusAsync_ShouldFilterByStatus()
    {
        await using var context = new TurniejeProDbContext(_options);
        var repository = new TeamRepository(context);

        var tournament = new Tournament("Turniej");
        var teamAccepted = new Team("Accepted", tournament.Id);
        teamAccepted.Accept();
        var teamPending = new Team("Pending", tournament.Id);
        var teamRejected = new Team("Rejected", tournament.Id);
        teamRejected.Reject();

        context.Tournaments.Add(tournament);
        context.Teams.AddRange(teamAccepted, teamPending, teamRejected);
        await context.SaveChangesAsync();

        var accepted = await repository.GetByStatusAsync(tournament.Id, TeamRegistrationStatus.Accepted);
        var pending = await repository.GetByStatusAsync(tournament.Id, TeamRegistrationStatus.Pending);
        var rejected = await repository.GetByStatusAsync(tournament.Id, TeamRegistrationStatus.Rejected);

        Assert.Single(accepted);
        Assert.Equal("Accepted", accepted[0].Name);
        Assert.Single(pending);
        Assert.Equal("Pending", pending[0].Name);
        Assert.Single(rejected);
        Assert.Equal("Rejected", rejected[0].Name);
    }

    [Fact]
    public async Task TeamRepository_ExistsByNameInTournamentAsync_ShouldReturnCorrectStatus()
    {
        await using var context = new TurniejeProDbContext(_options);
        var repository = new TeamRepository(context);

        var tournament = new Tournament("Turniej");
        var team = new Team("Unikalna", tournament.Id);

        context.Tournaments.Add(tournament);
        context.Teams.Add(team);
        await context.SaveChangesAsync();

        var exists = await repository.ExistsByNameInTournamentAsync(tournament.Id, "Unikalna");
        var notExists = await repository.ExistsByNameInTournamentAsync(tournament.Id, "Inna");

        Assert.True(exists);
        Assert.False(notExists);
    }

    [Fact]
    public async Task UnitOfWork_ShouldPerformTransactionalOperationsAcrossRepositories()
    {
        await using var context = new TurniejeProDbContext(_options);
        var uow = new UnitOfWork(context);

        var tournament = new Tournament("Turniej UoW");
        var team = new Team("Drużyna UoW", tournament.Id);
        var match = new Match(tournament.Id, 1);
        var token = new CollectibleToken("UOW1", "Token UoW", tournament.Id);

        await uow.Tournaments.AddAsync(tournament);
        await uow.Teams.AddAsync(team);
        await uow.Matches.AddAsync(match);
        await uow.Tokens.AddAsync(token);
        var affectedRows = await uow.SaveChangesAsync();

        Assert.True(affectedRows > 0);

        var savedTournament = await uow.Tournaments.GetByIdAsync(tournament.Id);
        var savedTeam = await uow.Teams.GetByIdAsync(team.Id);
        var savedMatch = await uow.Matches.GetByIdAsync(match.Id);
        var savedToken = await uow.Tokens.GetByIdAsync(token.Id);

        Assert.NotNull(savedTournament);
        Assert.NotNull(savedTeam);
        Assert.NotNull(savedMatch);
        Assert.NotNull(savedToken);
    }
}
