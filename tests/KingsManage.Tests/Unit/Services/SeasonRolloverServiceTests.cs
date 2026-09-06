using KingsManage;
using KingsManage.Tests.Fakes;
using KingsManage.Web.Services;

namespace KingsManage.Tests.Unit.Services;

public class SeasonRolloverServiceTests
{
	[Test]
	public async Task Preview_ShouldExcludeFriendliesAndBlockOutstandingMatches()
	{
		var setup = CreateSetup(isSeasonActive: false);
		setup.Matches.Add(CreateMatch(setup.Season.Id, setup.Player.Id, "League", true));
		setup.Matches.Add(CreateMatch(setup.Season.Id, setup.Player.Id, "Friendly", true));
		setup.Matches.Add(CreateMatch(setup.Season.Id, setup.Player.Id, "Cup", false));

		var preview = await setup.Service.GetPreviewAsync(setup.Season.Id);

		Assert.That(preview, Is.Not.Null);
		Assert.Multiple(() =>
		{
			Assert.That(preview!.CompletedCompetitiveMatches, Is.EqualTo(1));
			Assert.That(preview.IncompleteCompetitiveMatches, Is.EqualTo(1));
			Assert.That(preview.CanRollOver, Is.False);
			Assert.That(preview.AppearancesToAdd, Is.EqualTo(1));
			Assert.That(preview.GoalsToAdd, Is.EqualTo(1));
			Assert.That(preview.Players.Single().CareerAppsAfter, Is.EqualTo(11));
			Assert.That(preview.Players.Single().CareerGoalsAfter, Is.EqualTo(3));
		});
	}

	[Test]
	public async Task RollOver_ShouldCreateOneImmutableSnapshot()
	{
		var setup = CreateSetup(isSeasonActive: false);
		setup.Matches.Add(CreateMatch(setup.Season.Id, setup.Player.Id, "League", true));

		var firstResult = await setup.Service.RollOverAsync(setup.Season.Id);
		var secondResult = await setup.Service.RollOverAsync(setup.Season.Id);

		Assert.That(firstResult, Is.Not.Null);
		Assert.That(secondResult, Is.Not.Null);
		Assert.Multiple(() =>
		{
			Assert.That(firstResult!.IsAlreadyRolledOver, Is.True);
			Assert.That(secondResult!.IsAlreadyRolledOver, Is.True);
			Assert.That(setup.RolloverStore.ApplyCount, Is.EqualTo(1));
			Assert.That(setup.RolloverStore.Stored!.Players.Single().CareerAppsAfter, Is.EqualTo(11));
			Assert.That(setup.RolloverStore.Stored.Players.Single().CareerGoalsAfter, Is.EqualTo(3));
		});
	}

	[Test]
	public void RollOver_WhenSeasonIsActive_ShouldRejectTheRequest()
	{
		var setup = CreateSetup(isSeasonActive: true);
		setup.Matches.Add(CreateMatch(setup.Season.Id, setup.Player.Id, "League", true));

		var exception = Assert.ThrowsAsync<InvalidOperationException>(
			async () => await setup.Service.RollOverAsync(setup.Season.Id));

		Assert.That(exception!.Message, Does.Contain("Activate the next season"));
	}

	[Test]
	public async Task Breakdown_ShouldShowTheBaselineAndEveryMatchContribution()
	{
		var setup = CreateSetup(isSeasonActive: false);
		setup.Matches.Add(CreateMatch(setup.Season.Id, setup.Player.Id, "League", true));
		await setup.Service.RollOverAsync(setup.Season.Id);

		var breakdown = await setup.Service.GetPlayerBreakdownAsync(
			setup.Season.Id,
			setup.Player.Id);

		Assert.That(breakdown, Is.Not.Null);
		Assert.Multiple(() =>
		{
			Assert.That(breakdown!.IsSeasonRolledOver, Is.True);
			Assert.That(breakdown.HistoricalApps, Is.EqualTo(10));
			Assert.That(breakdown.SeasonApps, Is.EqualTo(1));
			Assert.That(breakdown.CareerApps, Is.EqualTo(11));
			Assert.That(breakdown.Matches, Has.Count.EqualTo(1));
			Assert.That(breakdown.Matches[0].Goals, Is.EqualTo(1));
			Assert.That(breakdown.Matches[0].Assists, Is.EqualTo(1));
			Assert.That(breakdown.Matches[0].Minutes, Is.EqualTo(90));
		});
	}

	[Test]
	public async Task PlayerStatsQuery_ShouldUseTheFrozenSnapshotForAClosedSeason()
	{
		var setup = CreateSetup(isSeasonActive: false);
		setup.Matches.Add(CreateMatch(setup.Season.Id, setup.Player.Id, "League", true));
		await setup.Service.RollOverAsync(setup.Season.Id);
		setup.Stats.HistoricalStats[0].Appearances = 999;
		var query = new PlayerStatsQueryService(
			setup.MatchService,
			setup.PlayerService,
			setup.Stats,
			setup.RolloverStore);

		var rows = await query.BuildRowsAsync(setup.Season.Id, includeFriendlies: false);

		Assert.That(rows, Has.Count.EqualTo(1));
		Assert.Multiple(() =>
		{
			Assert.That(rows[0].PreSeasonApps, Is.EqualTo(10));
			Assert.That(rows[0].SeasonApps, Is.EqualTo(1));
			Assert.That(rows[0].CareerApps, Is.EqualTo(11));
		});
	}

	private static TestSetup CreateSetup(bool isSeasonActive)
	{
		var season = new Season
		{
			Id = Guid.NewGuid(),
			Name = "2026/27",
			IsActive = isSeasonActive
		};
		var player = new Player
		{
			Id = Guid.NewGuid(),
			Name = "Player One",
			IsActive = true
		};
		var matches = new List<Match>();
		var matchService = new FakeMatchService(matches);
		var playerService = new FakePlayerService([player]);
		var stats = new FakeStatsService();
		stats.HistoricalStats.Add(new PlayerHistoricalStats
		{
			PlayerId = player.Id,
			Appearances = 10,
			Goals = 2
		});
		var rolloverStore = new FakeRolloverStore();
		var service = new SeasonRolloverService(
			matchService,
			playerService,
			rolloverStore,
			new FakeSeasonService(season),
			stats);

		return new TestSetup(
			season,
			player,
			matches,
			matchService,
			playerService,
			stats,
			rolloverStore,
			service);
	}

	private static Match CreateMatch(
		Guid seasonId,
		Guid playerId,
		string competition,
		bool isCompleted)
	{
		return new Match
		{
			Id = Guid.NewGuid(),
			SeasonId = seasonId,
			Team = ClubTeam.First,
			Opponent = "Rovers",
			Competition = competition,
			Date = DateTime.UtcNow.AddDays(-1),
			Venue = MatchVenue.Home,
			IsCompleted = isCompleted,
			State = isCompleted ? MatchState.Won : MatchState.Upcoming,
			Result = isCompleted ? new MatchResult { HomeGoals = 2, AwayGoals = 0 } : null,
			SelectedPlayers = [new SelectedPlayer { PlayerId = playerId, Area = "pitch" }],
			PlayerStats =
			[
				new MatchPlayerStats
				{
					PlayerId = playerId,
					AppearanceType = MatchAppearanceType.Started,
					Goals = 1,
					Assists = 1,
					Minutes = 90
				}
			]
		};
	}

	private sealed record TestSetup(
		Season Season,
		Player Player,
		List<Match> Matches,
		FakeMatchService MatchService,
		FakePlayerService PlayerService,
		FakeStatsService Stats,
		FakeRolloverStore RolloverStore,
		SeasonRolloverService Service);

	private sealed class FakeRolloverStore : ISeasonRolloverStore
	{
		public SeasonRollover? Stored { get; private set; }
		public int ApplyCount { get; private set; }

		public Task<SeasonRollover?> GetBySeasonIdAsync(
			Guid seasonId,
			CancellationToken cancellationToken = default) =>
			Task.FromResult(Stored?.SeasonId == seasonId ? Stored : null);

		public Task<SeasonRollover> ApplyAsync(
			SeasonRollover rollover,
			CancellationToken cancellationToken = default)
		{
			if (Stored is not null) return Task.FromResult(Stored);
			ApplyCount++;
			rollover.RolledOverAt = DateTime.UtcNow;
			Stored = rollover;
			return Task.FromResult(rollover);
		}
	}

	private sealed class FakeSeasonService(Season season) : ISeasonService
	{
		public Task<IReadOnlyList<Season>> GetAllAsync(CancellationToken cancellationToken = default) =>
			Task.FromResult<IReadOnlyList<Season>>([season]);
		public Task<Season?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
			Task.FromResult(id == season.Id ? season : null);
		public Task<Season?> GetActiveAsync(CancellationToken cancellationToken = default) =>
			Task.FromResult(season.IsActive ? season : null);
		public Task<Season> CreateAsync(Season value, CancellationToken cancellationToken = default) =>
			Task.FromResult(value);
		public Task<Season?> UpdateAsync(Season value, CancellationToken cancellationToken = default) =>
			Task.FromResult<Season?>(value);
		public Task<Season?> SetActiveAsync(Guid id, CancellationToken cancellationToken = default) =>
			Task.FromResult<Season?>(id == season.Id ? season : null);
	}

	private sealed class FakePlayerService(List<Player> players) : IPlayerService
	{
		public Task<IReadOnlyList<Player>> GetAllAsync(CancellationToken cancellationToken = default) =>
			Task.FromResult<IReadOnlyList<Player>>(players);
		public Task<Player?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
			Task.FromResult(players.FirstOrDefault(player => player.Id == id));
		public Task<Player> CreateAsync(Player player, CancellationToken cancellationToken = default) =>
			Task.FromResult(player);
		public Task<Player?> UpdateAsync(Player player, CancellationToken cancellationToken = default) =>
			Task.FromResult<Player?>(player);
		public Task<Player?> SetActiveAsync(Guid id, bool isActive, CancellationToken cancellationToken = default) =>
			Task.FromResult(players.FirstOrDefault(player => player.Id == id));
	}

	private sealed class FakeMatchService(List<Match> matches) : IMatchService
	{
		public Task<IReadOnlyList<Match>> GetAllAsync(CancellationToken cancellationToken = default) =>
			Task.FromResult<IReadOnlyList<Match>>(matches);
		public Task<IReadOnlyList<Match>> GetBySeasonAsync(Guid seasonId, CancellationToken cancellationToken = default) =>
			Task.FromResult<IReadOnlyList<Match>>(matches.Where(match => match.SeasonId == seasonId).ToList());
		public Task<Match?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
			Task.FromResult(matches.FirstOrDefault(match => match.Id == id));
		public Task<Match> CreateAsync(Match match, CancellationToken cancellationToken = default) => Task.FromResult(match);
		public Task<Match?> UpdateAsync(Match match, CancellationToken cancellationToken = default) => Task.FromResult<Match?>(match);
		public Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult(true);
		public Task<Match?> SetResultAsync(Guid id, MatchResult result, CancellationToken cancellationToken = default) => Task.FromResult<Match?>(null);
		public Task<Match?> ClearResultAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult<Match?>(null);
		public Task<Match?> SetSelectedPlayersAsync(Guid id, List<SelectedPlayer> selectedPlayers, CancellationToken cancellationToken = default) => Task.FromResult<Match?>(null);
		public Task<Match?> SetLineupFormationAsync(Guid id, LineupFormation formation, CancellationToken cancellationToken = default) => Task.FromResult<Match?>(null);
		public Task<Match?> ToggleLineupLockedAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult<Match?>(null);
		public Task<Match?> UpdateNotesAsync(Guid id, MatchNotes notes, CancellationToken cancellationToken = default) => Task.FromResult<Match?>(null);
		public Task<Match?> UpdatePlayerStatsAsync(Guid id, List<MatchPlayerStats> playerStats, CancellationToken cancellationToken = default) => Task.FromResult<Match?>(null);
		public Task<Match?> PostponeAsync(Guid id, DateTime newDate, string? reason, CancellationToken cancellationToken = default) => Task.FromResult<Match?>(null);
		public Task<Match?> RestoreAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult<Match?>(null);
	}
}
