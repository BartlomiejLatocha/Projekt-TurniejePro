using TurniejePro.Domain;
using TurniejePro.Domain.Enums;
using TurniejePro.Domain.Repositories;
using TurniejePro.Domain.Scheduling;
using Xunit;

namespace TurniejePro.Tests;

public class MatchResultServiceTests
{
    [Fact]
    public async Task SetResultAsync_LeagueWin_UpdatesScoreAndFinishesMatch()
    {
        // Arrange
        var tournament = new Tournament("Liga", "Opis", TournamentFormat.League, DateTimeOffset.UtcNow.AddDays(1), 8, null);
        var team1 = CreateAcceptedTeam("Drużyna 1", tournament.Id);
        var team2 = CreateAcceptedTeam("Drużyna 2", tournament.Id);
        var match = new Match(tournament.Id, 1, 1, team1.Id, team2.Id);

        var fakeUow = new FakeUnitOfWork(tournament, [team1, team2], [match]);
        var sut = new MatchResultService(fakeUow);

        // Act
        var result = await sut.SetResultAsync(tournament.Id, match.Id, 2, 1);

        // Assert
        Assert.Equal(MatchStatus.Finished, result.Status);
        Assert.Equal(2, result.HomeScore);
        Assert.Equal(1, result.AwayScore);
    }

    [Fact]
    public async Task SetResultAsync_LeagueDraw_AllowsDraw()
    {
        // Arrange
        var tournament = new Tournament("Liga", "Opis", TournamentFormat.League, DateTimeOffset.UtcNow.AddDays(1), 8, null);
        var team1 = CreateAcceptedTeam("T1", tournament.Id);
        var team2 = CreateAcceptedTeam("T2", tournament.Id);
        var match = new Match(tournament.Id, 1, 1, team1.Id, team2.Id);

        var fakeUow = new FakeUnitOfWork(tournament, [team1, team2], [match]);
        var sut = new MatchResultService(fakeUow);

        // Act
        var result = await sut.SetResultAsync(tournament.Id, match.Id, 1, 1);

        // Assert
        Assert.Equal(MatchStatus.Finished, result.Status);
        Assert.Equal(1, result.HomeScore);
        Assert.Equal(1, result.AwayScore);
    }

    [Fact]
    public async Task SetResultAsync_NegativeScore_ThrowsArgumentException()
    {
        // Arrange
        var tournament = new Tournament("Liga", "Opis", TournamentFormat.League, DateTimeOffset.UtcNow.AddDays(1), 8, null);
        var team1 = CreateAcceptedTeam("T1", tournament.Id);
        var team2 = CreateAcceptedTeam("T2", tournament.Id);
        var match = new Match(tournament.Id, 1, 1, team1.Id, team2.Id);

        var fakeUow = new FakeUnitOfWork(tournament, [team1, team2], [match]);
        var sut = new MatchResultService(fakeUow);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => sut.SetResultAsync(tournament.Id, match.Id, -1, 0));
    }

    [Fact]
    public async Task SetResultAsync_NonExistentTournamentOrMatch_ThrowsKeyNotFoundException()
    {
        // Arrange
        var tournament = new Tournament("Liga", "Opis", TournamentFormat.League, DateTimeOffset.UtcNow.AddDays(1), 8, null);
        var fakeUow = new FakeUnitOfWork(tournament, [], []);
        var sut = new MatchResultService(fakeUow);

        // Act & Assert: Nieistniejący turniej
        await Assert.ThrowsAsync<KeyNotFoundException>(() => sut.SetResultAsync(Guid.NewGuid(), Guid.NewGuid(), 1, 0));

        // Act & Assert: Nieistniejący mecz w prawidłowym turnieju
        await Assert.ThrowsAsync<KeyNotFoundException>(() => sut.SetResultAsync(tournament.Id, Guid.NewGuid(), 1, 0));
    }

    [Fact]
    public async Task SetResultAsync_CupDraw_ThrowsInvalidOperationException()
    {
        // Arrange
        var tournament = new Tournament("Puchar", "Opis", TournamentFormat.Cup, DateTimeOffset.UtcNow.AddDays(1), 8, null);
        var team1 = CreateAcceptedTeam("T1", tournament.Id);
        var team2 = CreateAcceptedTeam("T2", tournament.Id);
        var match = new Match(tournament.Id, 1, 1, team1.Id, team2.Id);

        var fakeUow = new FakeUnitOfWork(tournament, [team1, team2], [match]);
        var sut = new MatchResultService(fakeUow);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => sut.SetResultAsync(tournament.Id, match.Id, 2, 2));
    }

    [Fact]
    public async Task SetResultAsync_Cup4Teams_Match1Win_AdvancesWinnerAsHomeTeamInFinal()
    {
        // Arrange: 4 drużyny pucharowe -> Mecz 1 (Runda 1, Poz 1), Mecz 2 (Runda 1, Poz 2), Finał (Runda 2, Poz 1)
        var tournament = new Tournament("Puchar 4", "Opis", TournamentFormat.Cup, DateTimeOffset.UtcNow.AddDays(1), 4, null);
        var t1 = CreateAcceptedTeam("T1", tournament.Id);
        var t2 = CreateAcceptedTeam("T2", tournament.Id);

        var match1 = new Match(tournament.Id, 1, 1, t1.Id, t2.Id);
        var finalMatch = new Match(tournament.Id, 2, 1, homeTeamId: null, awayTeamId: null);

        var fakeUow = new FakeUnitOfWork(tournament, [t1, t2], [match1, finalMatch]);
        var sut = new MatchResultService(fakeUow);

        // Act: T1 wygrywa mecz 1 (nieparzysta pozycja 1 -> ląduje jako Gospodarz w finale)
        await sut.SetResultAsync(tournament.Id, match1.Id, 3, 0);

        // Assert
        Assert.Equal(t1.Id, finalMatch.HomeTeamId);
        Assert.Null(finalMatch.AwayTeamId);
    }

    [Fact]
    public async Task SetResultAsync_Cup4Teams_Match2Win_AdvancesWinnerAsAwayTeamInFinal()
    {
        // Arrange
        var tournament = new Tournament("Puchar 4", "Opis", TournamentFormat.Cup, DateTimeOffset.UtcNow.AddDays(1), 4, null);
        var t3 = CreateAcceptedTeam("T3", tournament.Id);
        var t4 = CreateAcceptedTeam("T4", tournament.Id);

        var match2 = new Match(tournament.Id, 1, 2, t3.Id, t4.Id);
        var finalMatch = new Match(tournament.Id, 2, 1, homeTeamId: null, awayTeamId: null);

        var fakeUow = new FakeUnitOfWork(tournament, [t3, t4], [match2, finalMatch]);
        var sut = new MatchResultService(fakeUow);

        // Act: T4 wygrywa mecz 2 (parzysta pozycja 2 -> ląduje jako Gość w finale)
        await sut.SetResultAsync(tournament.Id, match2.Id, 0, 1);

        // Assert
        Assert.Equal(t4.Id, finalMatch.AwayTeamId);
        Assert.Null(finalMatch.HomeTeamId);
    }

    [Fact]
    public async Task SetResultAsync_EditResultChangesWinner_WhenFinalNotStarted_ReplacesTeamInFinal()
    {
        // Arrange
        var tournament = new Tournament("Puchar 4", "Opis", TournamentFormat.Cup, DateTimeOffset.UtcNow.AddDays(1), 4, null);
        var t1 = CreateAcceptedTeam("T1", tournament.Id);
        var t2 = CreateAcceptedTeam("T2", tournament.Id);

        var match1 = new Match(tournament.Id, 1, 1, t1.Id, t2.Id);
        var finalMatch = new Match(tournament.Id, 2, 1, homeTeamId: null, awayTeamId: null);

        var fakeUow = new FakeUnitOfWork(tournament, [t1, t2], [match1, finalMatch]);
        var sut = new MatchResultService(fakeUow);

        // 1. T1 wygrywa
        await sut.SetResultAsync(tournament.Id, match1.Id, 2, 0);
        Assert.Equal(t1.Id, finalMatch.HomeTeamId);

        // 2. Korekta wyniku na wygraną T2
        await sut.SetResultAsync(tournament.Id, match1.Id, 0, 1);

        // Assert: T2 podmienia T1 w finale
        Assert.Equal(t2.Id, finalMatch.HomeTeamId);
    }

    [Fact]
    public async Task SetResultAsync_EditResult_WhenFinalAlreadyInProgressOrFinished_ThrowsMatchResultConflictException()
    {
        // Arrange
        var tournament = new Tournament("Puchar 4", "Opis", TournamentFormat.Cup, DateTimeOffset.UtcNow.AddDays(1), 4, null);
        var t1 = CreateAcceptedTeam("T1", tournament.Id);
        var t2 = CreateAcceptedTeam("T2", tournament.Id);

        var match1 = new Match(tournament.Id, 1, 1, t1.Id, t2.Id);
        var finalMatch = new Match(tournament.Id, 2, 1, t1.Id, Guid.NewGuid());
        finalMatch.StartMatch(); // Finał już w toku

        var fakeUow = new FakeUnitOfWork(tournament, [t1, t2], [match1, finalMatch]);
        var sut = new MatchResultService(fakeUow);

        // Act & Assert: Próba edycji wyników półfinału po rozpoczęciu finału
        await Assert.ThrowsAsync<MatchResultConflictException>(() => sut.SetResultAsync(tournament.Id, match1.Id, 1, 2));
    }

    [Fact]
    public async Task SetResultAsync_ByeMatch_ThrowsMatchResultConflictException()
    {
        // Arrange
        var tournament = new Tournament("Puchar 5", "Opis", TournamentFormat.Cup, DateTimeOffset.UtcNow.AddDays(1), 5, null);
        var t1 = CreateAcceptedTeam("T1", tournament.Id);

        var byeMatch = new Match(tournament.Id, 1, 1, t1.Id, awayTeamId: null);
        byeMatch.MarkAsBye();

        var fakeUow = new FakeUnitOfWork(tournament, [t1], [byeMatch]);
        var sut = new MatchResultService(fakeUow);

        // Act & Assert
        await Assert.ThrowsAsync<MatchResultConflictException>(() => sut.SetResultAsync(tournament.Id, byeMatch.Id, 1, 0));
    }

    [Fact]
    public async Task SetResultAsync_MatchWithoutAssignedTeams_ThrowsMatchResultConflictException()
    {
        // Arrange: Mecz finałowy bez obydwu drużyn
        var tournament = new Tournament("Puchar 4", "Opis", TournamentFormat.Cup, DateTimeOffset.UtcNow.AddDays(1), 4, null);
        var unassignedMatch = new Match(tournament.Id, 2, 1, homeTeamId: null, awayTeamId: null);

        var fakeUow = new FakeUnitOfWork(tournament, [], [unassignedMatch]);
        var sut = new MatchResultService(fakeUow);

        // Act & Assert
        await Assert.ThrowsAsync<MatchResultConflictException>(() => sut.SetResultAsync(tournament.Id, unassignedMatch.Id, 1, 0));
    }

    [Fact]
    public async Task SetResultAsync_FinalMatch_SucceedsWithoutErrorWhenNoNextMatchExists()
    {
        // Arrange
        var tournament = new Tournament("Puchar", "Opis", TournamentFormat.Cup, DateTimeOffset.UtcNow.AddDays(1), 2, null);
        var t1 = CreateAcceptedTeam("T1", tournament.Id);
        var t2 = CreateAcceptedTeam("T2", tournament.Id);

        var finalMatch = new Match(tournament.Id, 1, 1, t1.Id, t2.Id);

        var fakeUow = new FakeUnitOfWork(tournament, [t1, t2], [finalMatch]);
        var sut = new MatchResultService(fakeUow);

        // Act
        var result = await sut.SetResultAsync(tournament.Id, finalMatch.Id, 3, 2);

        // Assert
        Assert.Equal(MatchStatus.Finished, result.Status);
        Assert.Equal(3, result.HomeScore);
        Assert.Equal(2, result.AwayScore);
    }

    private static Team CreateAcceptedTeam(string name, Guid tournamentId)
    {
        var team = new Team(name, tournamentId);
        team.Accept();
        return team;
    }

    #region Fakes implementation

    internal sealed class FakeUnitOfWork : IUnitOfWork
    {
        private readonly FakeTournamentRepository _tournamentsRepo;
        private readonly FakeTeamRepository _teamsRepo;
        private readonly FakeMatchRepository _matchesRepo;

        public ITournamentRepository Tournaments => _tournamentsRepo;
        public ITeamRepository Teams => _teamsRepo;
        public IMatchRepository Matches => _matchesRepo;
        public IRepository<CollectibleToken> Tokens => throw new NotImplementedException();

        public FakeUnitOfWork(Tournament tournament, List<Team> teams, List<Match> matches)
        {
            _tournamentsRepo = new FakeTournamentRepository(tournament);
            _teamsRepo = new FakeTeamRepository(teams);
            _matchesRepo = new FakeMatchRepository(matches);
        }

        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(1);
        }
    }

    internal sealed class FakeTournamentRepository : ITournamentRepository
    {
        private readonly List<Tournament> _tournaments = [];

        public FakeTournamentRepository(Tournament? tournament)
        {
            if (tournament is not null)
            {
                _tournaments.Add(tournament);
            }
        }

        public Task<Tournament?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
            => Task.FromResult(_tournaments.FirstOrDefault(t => t.Id == id));

        public Task<Tournament?> GetWithDetailsAsync(Guid id, CancellationToken cancellationToken = default)
            => Task.FromResult(_tournaments.FirstOrDefault(t => t.Id == id));

        public Task<IReadOnlyList<Tournament>> GetByOrganizerIdAsync(Guid organizerId, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<Tournament>>(_tournaments.Where(t => t.OrganizerId == organizerId).ToList());

        public Task<(IReadOnlyList<Tournament> Items, int TotalCount)> GetPagedAsync(int pageNumber, int pageSize, string? searchTerm = null, CancellationToken cancellationToken = default)
            => Task.FromResult<(IReadOnlyList<Tournament> Items, int TotalCount)>((_tournaments.ToList(), _tournaments.Count));

        public Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult(_tournaments.Any(t => t.Id == id));
        public Task AddAsync(Tournament entity, CancellationToken cancellationToken = default) { _tournaments.Add(entity); return Task.CompletedTask; }
        public Task<IReadOnlyList<Tournament>> GetAllAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<Tournament>>(_tournaments.ToList());

        public void Update(Tournament entity)
        {
            _tournaments.RemoveAll(t => t.Id == entity.Id);
            _tournaments.Add(entity);
        }

        public void Delete(Tournament entity)
        {
            _tournaments.RemoveAll(t => t.Id == entity.Id);
        }

        public void Remove(Tournament entity)
        {
            _tournaments.RemoveAll(t => t.Id == entity.Id);
        }
    }

    internal sealed class FakeTeamRepository : ITeamRepository
    {
        private readonly List<Team> _teams;
        public FakeTeamRepository(List<Team> teams) => _teams = teams;

        public Task<IReadOnlyList<Team>> GetByTournamentIdAndStatusAsync(Guid tournamentId, TeamRegistrationStatus status, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<Team>>(_teams.Where(t => t.TournamentId == tournamentId && t.Status == status).ToList());

        public Task<IReadOnlyList<Team>> GetByTournamentIdAsync(Guid tournamentId, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<Team>>(_teams.Where(t => t.TournamentId == tournamentId).ToList());

        public Task<IReadOnlyList<Team>> GetByStatusAsync(Guid tournamentId, TeamRegistrationStatus status, CancellationToken cancellationToken = default)
            => GetByTournamentIdAndStatusAsync(tournamentId, status, cancellationToken);

        public Task<IReadOnlyList<Team>> GetByCaptainIdAsync(Guid captainId, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<Team>>(_teams.Where(t => t.CaptainId == captainId).ToList());

        public Task<Team?> GetWithPlayersAsync(Guid id, CancellationToken cancellationToken = default)
            => Task.FromResult(_teams.FirstOrDefault(t => t.Id == id));

        public Task<bool> ExistsByNameInTournamentAsync(Guid tournamentId, string name, CancellationToken cancellationToken = default)
            => Task.FromResult(_teams.Any(t => t.TournamentId == tournamentId && t.Name == name));

        public Task<Team?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
            => Task.FromResult(_teams.FirstOrDefault(t => t.Id == id));

        public Task AddAsync(Team entity, CancellationToken cancellationToken = default) { _teams.Add(entity); return Task.CompletedTask; }
        public Task<IReadOnlyList<Team>> GetAllAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<Team>>(_teams.ToList());

        public void Update(Team entity)
        {
            _teams.RemoveAll(t => t.Id == entity.Id);
            _teams.Add(entity);
        }

        public void Delete(Team entity)
        {
            _teams.RemoveAll(t => t.Id == entity.Id);
        }

        public void Remove(Team entity)
        {
            _teams.RemoveAll(t => t.Id == entity.Id);
        }
    }

    internal sealed class FakeMatchRepository : IMatchRepository
    {
        private readonly List<Match> _matches;
        public FakeMatchRepository(List<Match> matches) => _matches = matches;

        public Task<bool> HasScheduleAsync(Guid tournamentId, CancellationToken cancellationToken = default)
            => Task.FromResult(_matches.Any(m => m.TournamentId == tournamentId));

        public Task<IReadOnlyList<Match>> GetByTournamentIdAsync(Guid tournamentId, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<Match>>(_matches.Where(m => m.TournamentId == tournamentId).OrderBy(m => m.Round).ThenBy(m => m.Position).ToList());

        public Task DeleteByTournamentIdAsync(Guid tournamentId, CancellationToken cancellationToken = default)
        {
            _matches.RemoveAll(m => m.TournamentId == tournamentId);
            return Task.CompletedTask;
        }

        public Task AddAsync(Match entity, CancellationToken cancellationToken = default) { _matches.Add(entity); return Task.CompletedTask; }
        public Task<Match?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult(_matches.FirstOrDefault(m => m.Id == id));
        public Task<IReadOnlyList<Match>> GetAllAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<Match>>(_matches.ToList());

        public void Update(Match entity)
        {
            _matches.RemoveAll(m => m.Id == entity.Id);
            _matches.Add(entity);
        }

        public void Delete(Match entity)
        {
            _matches.RemoveAll(m => m.Id == entity.Id);
        }

        public void Remove(Match entity)
        {
            _matches.RemoveAll(m => m.Id == entity.Id);
        }
    }

    #endregion
}
