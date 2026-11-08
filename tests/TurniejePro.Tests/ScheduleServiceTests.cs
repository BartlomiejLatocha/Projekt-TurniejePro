using TurniejePro.Domain;
using TurniejePro.Domain.Enums;
using TurniejePro.Domain.Repositories;
using TurniejePro.Domain.Scheduling;
using Xunit;

namespace TurniejePro.Tests;

public class ScheduleServiceTests
{
    private static readonly int[] ExpectedRound1Positions = [1, 2];

    [Fact]
    public async Task GenerateScheduleAsync_League4Teams_Generates6MatchesIn3RoundsWithCorrectPositions()
    {
        // Arrange
        var tournament = new Tournament("Liga 4", "Opis", TournamentFormat.League, DateTimeOffset.UtcNow.AddDays(1), 16, null);
        var tournamentId = tournament.Id;
        var teams = CreateTeams(tournamentId, 4);
        var fakeUow = new FakeUnitOfWork(tournament, teams);
        var sut = new ScheduleService(fakeUow);

        // Act
        var result = await sut.GenerateScheduleAsync(tournamentId);

        // Assert
        Assert.Equal(6, result.Count);
        Assert.Equal(3, result.Select(m => m.Round).Distinct().Count());

        var round1Positions = result.Where(m => m.Round == 1).Select(m => m.Position).OrderBy(p => p).ToList();
        Assert.Equal(ExpectedRound1Positions, round1Positions);
        Assert.Equal(6, fakeUow.MatchesList.Count);
    }

    [Fact]
    public async Task GenerateScheduleAsync_League5Teams_Generates10MatchesIn5RoundsWithoutRestingMatches()
    {
        // Arrange
        var tournament = new Tournament("Liga 5", "Opis", TournamentFormat.League, DateTimeOffset.UtcNow.AddDays(1), 16, null);
        var tournamentId = tournament.Id;
        var teams = CreateTeams(tournamentId, 5);
        var fakeUow = new FakeUnitOfWork(tournament, teams);
        var sut = new ScheduleService(fakeUow);

        // Act
        var result = await sut.GenerateScheduleAsync(tournamentId);

        // Assert
        Assert.Equal(10, result.Count);
        Assert.Equal(5, result.Select(m => m.Round).Distinct().Count());
        Assert.DoesNotContain(result, m => m.HomeTeamId == null || m.AwayTeamId == null);
    }

    [Fact]
    public async Task GenerateScheduleAsync_Cup4Teams_Generates3MatchesIn2Rounds()
    {
        // Arrange
        var tournament = new Tournament("Puchar 4", "Opis", TournamentFormat.Cup, DateTimeOffset.UtcNow.AddDays(1), 16, null);
        var tournamentId = tournament.Id;
        var teams = CreateTeams(tournamentId, 4);
        var fakeUow = new FakeUnitOfWork(tournament, teams);
        var sut = new ScheduleService(fakeUow);

        // Act
        var result = await sut.GenerateScheduleAsync(tournamentId);

        // Assert
        Assert.Equal(3, result.Count);
        Assert.Equal(2, result.Select(m => m.Round).Distinct().Count());
        Assert.DoesNotContain(result, m => m.IsBye);
    }

    [Fact]
    public async Task GenerateScheduleAsync_Cup5Teams_Generates7MatchesIn3RoundsWith3Byes()
    {
        // Arrange
        var tournament = new Tournament("Puchar 5", "Opis", TournamentFormat.Cup, DateTimeOffset.UtcNow.AddDays(1), 16, null);
        var tournamentId = tournament.Id;
        var teams = CreateTeams(tournamentId, 5);
        var fakeUow = new FakeUnitOfWork(tournament, teams);
        var sut = new ScheduleService(fakeUow);

        // Act
        var result = await sut.GenerateScheduleAsync(tournamentId);

        // Assert
        Assert.Equal(7, result.Count);
        Assert.Equal(3, result.Select(m => m.Round).Distinct().Count());
        Assert.Equal(3, result.Count(m => m.IsBye));
        Assert.All(result.Where(m => m.IsBye), m => Assert.Equal(MatchStatus.Finished, m.Status));
    }

    [Fact]
    public async Task GenerateScheduleAsync_WhenScheduleAlreadyExists_ThrowsScheduleAlreadyExistsException()
    {
        // Arrange
        var tournament = new Tournament("Test", "Opis", TournamentFormat.League, DateTimeOffset.UtcNow.AddDays(1), 16, null);
        var tournamentId = tournament.Id;
        var teams = CreateTeams(tournamentId, 4);
        var fakeUow = new FakeUnitOfWork(tournament, teams) { HasSchedule = true };
        var sut = new ScheduleService(fakeUow);

        // Act & Assert
        await Assert.ThrowsAsync<ScheduleAlreadyExistsException>(() => sut.GenerateScheduleAsync(tournamentId));
    }

    [Fact]
    public async Task GenerateScheduleAsync_WhenLessThan2Teams_ThrowsInvalidOperationException()
    {
        // Arrange
        var tournament = new Tournament("Test", "Opis", TournamentFormat.League, DateTimeOffset.UtcNow.AddDays(1), 16, null);
        var tournamentId = tournament.Id;
        var teams = CreateTeams(tournamentId, 1);
        var fakeUow = new FakeUnitOfWork(tournament, teams);
        var sut = new ScheduleService(fakeUow);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => sut.GenerateScheduleAsync(tournamentId));
    }

    private static List<Team> CreateTeams(Guid tournamentId, int count)
    {
        return Enumerable.Range(1, count)
            .Select(i =>
            {
                var team = new Team($"Drużyna {i}", tournamentId);
                team.Accept();
                return team;
            })
            .ToList();
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

        public List<Match> MatchesList => _matchesRepo.MatchesList;
        public bool HasSchedule { get => _matchesRepo.HasSchedule; set => _matchesRepo.HasSchedule = value; }

        public FakeUnitOfWork(Tournament tournament, List<Team> teams)
        {
            _tournamentsRepo = new FakeTournamentRepository(tournament);
            _teamsRepo = new FakeTeamRepository(teams);
            _matchesRepo = new FakeMatchRepository();
        }

        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(MatchesList.Count);
        }
    }

    internal sealed class FakeTournamentRepository : ITournamentRepository
    {
        private readonly List<Tournament> _tournaments = new();

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
        {
            var result = _tournaments.Where(t => t.OrganizerId == organizerId).ToList();
            return Task.FromResult<IReadOnlyList<Tournament>>(result);
        }

        public Task<(IReadOnlyList<Tournament> Items, int TotalCount)> GetPagedAsync(
            int pageNumber,
            int pageSize,
            string? searchTerm = null,
            CancellationToken cancellationToken = default)
        {
            var result = _tournaments.ToList();
            return Task.FromResult<(IReadOnlyList<Tournament> Items, int TotalCount)>((result, result.Count));
        }

        public Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default)
            => Task.FromResult(_tournaments.Any(t => t.Id == id));

        public Task AddAsync(Tournament entity, CancellationToken cancellationToken = default)
        {
            _tournaments.Add(entity);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<Tournament>> GetAllAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<Tournament>>(_tournaments.ToList());
        }

        public void Update(Tournament entity)
        {
            var existing = _tournaments.FirstOrDefault(t => t.Id == entity.Id);
            if (existing is not null)
            {
                _tournaments.Remove(existing);
                _tournaments.Add(entity);
            }
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

        public FakeTeamRepository(List<Team> teams)
        {
            _teams = teams;
        }

        public Task<IReadOnlyList<Team>> GetByTournamentIdAndStatusAsync(Guid tournamentId, TeamRegistrationStatus status, CancellationToken cancellationToken = default)
        {
            var result = _teams.Where(t => t.TournamentId == tournamentId && t.Status == status).ToList();
            return Task.FromResult<IReadOnlyList<Team>>(result);
        }

        public Task<IReadOnlyList<Team>> GetByTournamentIdAsync(Guid tournamentId, CancellationToken cancellationToken = default)
        {
            var result = _teams.Where(t => t.TournamentId == tournamentId).ToList();
            return Task.FromResult<IReadOnlyList<Team>>(result);
        }

        public Task<IReadOnlyList<Team>> GetByStatusAsync(Guid tournamentId, TeamRegistrationStatus status, CancellationToken cancellationToken = default)
            => GetByTournamentIdAndStatusAsync(tournamentId, status, cancellationToken);

        public Task<IReadOnlyList<Team>> GetByCaptainIdAsync(Guid captainId, CancellationToken cancellationToken = default)
        {
            var result = _teams.Where(t => t.CaptainId == captainId).ToList();
            return Task.FromResult<IReadOnlyList<Team>>(result);
        }

        public Task<Team?> GetWithPlayersAsync(Guid id, CancellationToken cancellationToken = default)
            => Task.FromResult(_teams.FirstOrDefault(t => t.Id == id));

        public Task<bool> ExistsByNameInTournamentAsync(Guid tournamentId, string name, CancellationToken cancellationToken = default)
            => Task.FromResult(_teams.Any(t => t.TournamentId == tournamentId && t.Name == name));

        public Task<Team?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
            => Task.FromResult(_teams.FirstOrDefault(t => t.Id == id));

        public Task AddAsync(Team entity, CancellationToken cancellationToken = default)
        {
            _teams.Add(entity);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<Team>> GetAllAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<Team>>(_teams.ToList());

        public void Update(Team entity)
        {
            var existing = _teams.FirstOrDefault(t => t.Id == entity.Id);
            if (existing is not null)
            {
                _teams.Remove(existing);
                _teams.Add(entity);
            }
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
        public List<Match> MatchesList { get; } = new();
        public bool HasSchedule { get; set; }

        public Task<bool> HasScheduleAsync(Guid tournamentId, CancellationToken cancellationToken = default)
            => Task.FromResult(HasSchedule || MatchesList.Any(m => m.TournamentId == tournamentId));

        public Task<IReadOnlyList<Match>> GetByTournamentIdAsync(Guid tournamentId, CancellationToken cancellationToken = default)
        {
            var list = MatchesList.Where(m => m.TournamentId == tournamentId).OrderBy(m => m.Round).ThenBy(m => m.Position).ToList();
            return Task.FromResult<IReadOnlyList<Match>>(list);
        }

        public Task DeleteByTournamentIdAsync(Guid tournamentId, CancellationToken cancellationToken = default)
        {
            MatchesList.RemoveAll(m => m.TournamentId == tournamentId);
            return Task.CompletedTask;
        }

        public Task AddAsync(Match entity, CancellationToken cancellationToken = default)
        {
            MatchesList.Add(entity);
            return Task.CompletedTask;
        }

        public Task<Match?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
            => Task.FromResult(MatchesList.FirstOrDefault(m => m.Id == id));

        public Task<IReadOnlyList<Match>> GetAllAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<Match>>(MatchesList.ToList());

        public void Update(Match entity)
        {
            var existing = MatchesList.FirstOrDefault(m => m.Id == entity.Id);
            if (existing is not null)
            {
                MatchesList.Remove(existing);
                MatchesList.Add(entity);
            }
        }

        public void Delete(Match entity)
        {
            MatchesList.RemoveAll(m => m.Id == entity.Id);
        }

        public void Remove(Match entity)
        {
            MatchesList.RemoveAll(m => m.Id == entity.Id);
        }
    }

    #endregion
}
