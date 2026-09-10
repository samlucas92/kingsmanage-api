using KingsManage.Web.Services;

namespace KingsManage.Tests.Unit.Services;

public sealed class LeagueEligibilityServiceTests
{
	private static readonly Guid SeasonId = Guid.NewGuid();
	private static readonly Guid[] Players = Enumerable.Range(0, 5).Select(_ => Guid.NewGuid()).ToArray();

	[Test]
	public async Task RecentAppearanceRuleRejectsMoreThanConfiguredMaximum()
	{
		var target = Match(DefaultClubTeams.SecondTeamId, new DateTime(2026, 9, 10), "League", false);
		var previous = Match(DefaultClubTeams.FirstTeamId, new DateTime(2026, 9, 3), "League", true, Players);
		var service = Service([Rule(LeagueRuleType.RecentHigherTeamAppearanceLimit, ["League"], ["League"], 3)], [target, previous]);

		var result = await service.EvaluateAsync(target, Players.Take(4).ToList());

		Assert.That(result.IsValid, Is.False);
		Assert.That(result.Rules.Single().SelectedCount, Is.EqualTo(4));
	}

	[Test]
	public async Task RecentAppearanceRuleIsExemptWhenHigherTeamPlaysSameDay()
	{
		var target = Match(DefaultClubTeams.SecondTeamId, new DateTime(2026, 9, 10, 15, 0, 0), "League", false);
		var previous = Match(DefaultClubTeams.FirstTeamId, new DateTime(2026, 9, 3), "League", true, Players);
		var sameDay = Match(DefaultClubTeams.FirstTeamId, new DateTime(2026, 9, 10, 14, 0, 0), "League", false);
		var service = Service([Rule(LeagueRuleType.RecentHigherTeamAppearanceLimit, ["League"], ["League"], 3, true)], [target, previous, sameDay]);

		var result = await service.EvaluateAsync(target, Players);

		Assert.That(result.IsValid, Is.True);
		Assert.That(result.Rules.Single().IsExempt, Is.True);
	}

	[Test]
	public async Task CupTieOnlyUsesExplicitlyConfiguredCompetition()
	{
		var target = Match(DefaultClubTeams.SecondTeamId, new DateTime(2026, 10, 1), "League Cup", false);
		var leagueCupPlayer = Players[0];
		var faCupPlayer = Players[1];
		var leagueCup = Match(DefaultClubTeams.FirstTeamId, new DateTime(2026, 9, 1), "League Cup", true, [leagueCupPlayer]);
		var faCup = Match(DefaultClubTeams.FirstTeamId, new DateTime(2026, 9, 8), "West Wales FA Cup", true, [faCupPlayer]);
		var service = Service([Rule(LeagueRuleType.CupTied, ["League Cup"], ["League Cup"])], [target, leagueCup, faCup]);

		var allowed = await service.EvaluateAsync(target, [faCupPlayer]);
		var blocked = await service.EvaluateAsync(target, [leagueCupPlayer]);

		Assert.That(allowed.IsValid, Is.True);
		Assert.That(blocked.IsValid, Is.False);
		Assert.That(blocked.Rules.Single().AffectedPlayerIds, Does.Not.Contain(faCupPlayer));
	}

	private static LeagueEligibilityService Service(List<LeagueRule> rules, List<Match> matches) =>
		new(new FakeRuleService(rules), new FakeMatchService(matches));

	private static LeagueRule Rule(LeagueRuleType type, List<string> source, List<string> target, int? max = null, bool exemption = false) => new()
	{
		Id = Guid.NewGuid(), Name = "Test rule", IsActive = true, RuleType = type,
		HigherTeamId = DefaultClubTeams.FirstTeamId, RestrictedTeamId = DefaultClubTeams.SecondTeamId,
		MaxPlayers = max, ExemptWhenHigherTeamPlaysSameDay = exemption,
		HigherTeamCompetitions = source, RestrictedTeamCompetitions = target
	};

	private static Match Match(Guid teamId, DateTime date, string competition, bool completed, IEnumerable<Guid>? players = null) => new()
	{
		Id = Guid.NewGuid(), SeasonId = SeasonId, TeamId = teamId, Date = DateTime.SpecifyKind(date, DateTimeKind.Utc),
		Competition = competition, IsCompleted = completed,
		PlayerStats = (players ?? []).Select(id => new MatchPlayerStats { PlayerId = id, AppearanceType = MatchAppearanceType.Started }).ToList()
	};

	private sealed class FakeRuleService(List<LeagueRule> rules) : ILeagueRuleService
	{
		public Task<IReadOnlyList<LeagueRule>> GetAllAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<LeagueRule>>(rules);
		public Task<LeagueRule?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult(rules.FirstOrDefault(rule => rule.Id == id));
		public Task<LeagueRule> CreateAsync(LeagueRule rule, CancellationToken cancellationToken = default) => Task.FromResult(rule);
		public Task<LeagueRule?> UpdateAsync(LeagueRule rule, CancellationToken cancellationToken = default) => Task.FromResult<LeagueRule?>(rule);
	}

	private sealed class FakeMatchService(List<Match> matches) : IMatchService
	{
		public Task<IReadOnlyList<Match>> GetAllAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<Match>>(matches);
		public Task<IReadOnlyList<Match>> GetBySeasonAsync(Guid seasonId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<Match>>(matches.Where(match => match.SeasonId == seasonId).ToList());
		public Task<Match?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult(matches.FirstOrDefault(match => match.Id == id));
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
