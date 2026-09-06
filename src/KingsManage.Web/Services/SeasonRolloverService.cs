using KingsManage.Web.Models;

namespace KingsManage.Web.Services;

public sealed class SeasonRolloverService : ISeasonRolloverService
{
	private readonly IMatchService matchService;
	private readonly IPlayerService playerService;
	private readonly ISeasonRolloverStore rolloverStore;
	private readonly ISeasonService seasonService;
	private readonly IStatsService statsService;

	public SeasonRolloverService(
		IMatchService matchService,
		IPlayerService playerService,
		ISeasonRolloverStore rolloverStore,
		ISeasonService seasonService,
		IStatsService statsService)
	{
		this.matchService = matchService;
		this.playerService = playerService;
		this.rolloverStore = rolloverStore;
		this.seasonService = seasonService;
		this.statsService = statsService;
	}

	public async Task<SeasonRolloverPreviewViewModel?> GetPreviewAsync(
		Guid seasonId,
		CancellationToken cancellationToken = default)
	{
		var candidate = await BuildCandidateAsync(seasonId, cancellationToken);
		return candidate is null ? null : ToPreview(candidate);
	}

	public async Task<SeasonRolloverPreviewViewModel?> RollOverAsync(
		Guid seasonId,
		CancellationToken cancellationToken = default)
	{
		var candidate = await BuildCandidateAsync(seasonId, cancellationToken);
		if (candidate is null)
		{
			return null;
		}

		if (candidate.ExistingRollover is not null)
		{
			return ToPreview(candidate);
		}

		var blockingReasons = GetBlockingReasons(candidate);
		if (blockingReasons.Count > 0)
		{
			throw new InvalidOperationException(string.Join(" ", blockingReasons));
		}

		var rollover = new SeasonRollover
		{
			SeasonId = candidate.Season.Id,
			SeasonName = candidate.Season.Name,
			CompletedCompetitiveMatches = candidate.CompletedMatches.Count,
			Players = candidate.Players
		};
		candidate.ExistingRollover = await rolloverStore.ApplyAsync(
			rollover,
			cancellationToken);

		return ToPreview(candidate);
	}

	public async Task<PlayerStatsBreakdownViewModel?> GetPlayerBreakdownAsync(
		Guid seasonId,
		Guid playerId,
		CancellationToken cancellationToken = default)
	{
		var candidate = await BuildCandidateAsync(seasonId, cancellationToken);
		if (candidate is null)
		{
			return null;
		}

		var snapshot = (candidate.ExistingRollover?.Players ?? candidate.Players)
			.FirstOrDefault(player => player.PlayerId == playerId);
		if (snapshot is null)
		{
			return null;
		}

		return new PlayerStatsBreakdownViewModel
		{
			SeasonId = candidate.Season.Id,
			SeasonName = candidate.Season.Name,
			PlayerId = snapshot.PlayerId,
			PlayerName = snapshot.PlayerName,
			IsSeasonRolledOver = candidate.ExistingRollover is not null,
			RolledOverAt = candidate.ExistingRollover?.RolledOverAt,
			HistoricalApps = snapshot.HistoricalAppsBefore,
			HistoricalGoals = snapshot.HistoricalGoalsBefore,
			SeasonApps = snapshot.SeasonApps,
			SeasonGoals = snapshot.SeasonGoals,
			CareerApps = snapshot.CareerAppsAfter,
			CareerGoals = snapshot.CareerGoalsAfter,
			Matches = snapshot.Matches
				.OrderBy(match => match.Date)
				.Select(ToMatchContribution)
				.ToList()
		};
	}

	private async Task<RolloverCandidate?> BuildCandidateAsync(
		Guid seasonId,
		CancellationToken cancellationToken)
	{
		var season = await seasonService.GetByIdAsync(seasonId, cancellationToken);
		if (season is null)
		{
			return null;
		}

		var existingRollover = await rolloverStore.GetBySeasonIdAsync(
			seasonId,
			cancellationToken);
		if (existingRollover is not null)
		{
			return new RolloverCandidate
			{
				Season = season,
				ExistingRollover = existingRollover,
				Players = existingRollover.Players
			};
		}

		var matches = await matchService.GetBySeasonAsync(seasonId, cancellationToken);
		var competitiveMatches = matches
			.Where(match => !MatchCompetition.IsFriendly(match.Competition))
			.ToList();
		var completedMatches = competitiveMatches
			.Where(match => match.IsCompleted)
			.ToList();
		var players = await playerService.GetAllAsync(cancellationToken);
		var historicalStats = await statsService.GetHistoricalStatsAsync(cancellationToken);
		var historicalByPlayerId = historicalStats
			.GroupBy(stats => stats.PlayerId)
			.ToDictionary(group => group.Key, group => group.First());
		var seasonStats = SeasonStatsCalculator.Calculate(seasonId, completedMatches);
		var snapshots = players
			.OrderBy(player => player.Name)
			.Select(player => BuildPlayerSnapshot(
				player,
				seasonStats.Where(stats => stats.PlayerId == player.Id).ToList(),
				historicalByPlayerId.GetValueOrDefault(player.Id),
				completedMatches))
			.ToList();

		return new RolloverCandidate
		{
			Season = season,
			CompletedMatches = completedMatches,
			IncompleteCompetitiveMatches = competitiveMatches.Count(match => !match.IsCompleted),
			Players = snapshots
		};
	}

	private static SeasonRolloverPlayerSnapshot BuildPlayerSnapshot(
		Player player,
		IReadOnlyList<PlayerSeasonStats> playerSeasonStats,
		PlayerHistoricalStats? historicalStats,
		IReadOnlyList<Match> completedMatches)
	{
		var historicalApps = historicalStats?.Appearances ?? 0;
		var historicalGoals = historicalStats?.Goals ?? 0;
		var seasonApps = playerSeasonStats.Sum(stats => stats.Appearances);
		var seasonGoals = playerSeasonStats.Sum(stats => stats.Goals);

		return new SeasonRolloverPlayerSnapshot
		{
			PlayerId = player.Id,
			PlayerName = player.Name,
			IsActive = player.IsActive,
			HistoricalAppsBefore = historicalApps,
			HistoricalGoalsBefore = historicalGoals,
			SeasonApps = seasonApps,
			SeasonGoals = seasonGoals,
			CareerAppsAfter = historicalApps + seasonApps,
			CareerGoalsAfter = historicalGoals + seasonGoals,
			Starts = playerSeasonStats.Sum(stats => stats.Starts),
			Bench = playerSeasonStats.Sum(stats => stats.Bench),
			UnusedSubstitutes = playerSeasonStats.Sum(stats => stats.UnusedSubstitutes),
			Assists = playerSeasonStats.Sum(stats => stats.Assists),
			Minutes = playerSeasonStats.Sum(stats => stats.Minutes),
			Motm = playerSeasonStats.Sum(stats => stats.Motm),
			YellowCards = playerSeasonStats.Sum(stats => stats.YellowCards),
			RedCards = playerSeasonStats.Sum(stats => stats.RedCards),
			TeamStats = playerSeasonStats.Select(stats => new SeasonRolloverTeamSnapshot
			{
				TeamId = stats.TeamId ?? DefaultClubTeams.FromLegacy(stats.Team),
				Team = stats.Team,
				Appearances = stats.Appearances,
				Goals = stats.Goals,
				Assists = stats.Assists,
				Minutes = stats.Minutes
			}).ToList(),
			Matches = completedMatches
				.Select(match => BuildMatchContribution(match, player.Id))
				.Where(match => match is not null)
				.Select(match => match!)
				.ToList()
		};
	}

	private static SeasonRolloverMatchContribution? BuildMatchContribution(
		Match match,
		Guid playerId)
	{
		var selectedPlayer = match.SelectedPlayers.FirstOrDefault(
			player => player.PlayerId == playerId);
		if (selectedPlayer is null)
		{
			return null;
		}

		var playerStats = match.PlayerStats.FirstOrDefault(stats => stats.PlayerId == playerId);
		var appearanceType = playerStats is not null &&
			playerStats.AppearanceType != MatchAppearanceType.Unspecified
				? playerStats.AppearanceType
				: selectedPlayer.Area.Equals("pitch", StringComparison.OrdinalIgnoreCase)
					? MatchAppearanceType.Started
					: MatchAppearanceType.SubstituteUsed;
		var countsAsAppearance = appearanceType is
			MatchAppearanceType.Started or MatchAppearanceType.SubstituteUsed;
		var includePlayerStats = countsAsAppearance && playerStats is not null;

		return new SeasonRolloverMatchContribution
		{
			MatchId = match.Id,
			Date = match.Date,
			TeamId = match.TeamId ?? DefaultClubTeams.FromLegacy(match.Team),
			Team = match.Team,
			Opponent = match.Opponent,
			Competition = MatchCompetition.DisplayName(match.Competition),
			Venue = match.Venue,
			HomeGoals = match.Result?.HomeGoals ?? 0,
			AwayGoals = match.Result?.AwayGoals ?? 0,
			AppearanceType = appearanceType,
			Appearances = countsAsAppearance ? 1 : 0,
			Goals = includePlayerStats ? Math.Max(playerStats!.Goals, 0) : 0,
			Assists = includePlayerStats ? Math.Max(playerStats!.Assists, 0) : 0,
			Minutes = includePlayerStats ? Math.Max(playerStats!.Minutes, 0) : 0,
			YellowCards = includePlayerStats ? Math.Max(playerStats!.YellowCards, 0) : 0,
			RedCards = includePlayerStats ? Math.Max(playerStats!.RedCards, 0) : 0,
			IsMotm = includePlayerStats && playerStats!.IsMOTM
		};
	}

	private static List<string> GetBlockingReasons(RolloverCandidate candidate)
	{
		var reasons = new List<string>();
		if (candidate.Season.IsActive)
		{
			reasons.Add("Activate the next season before closing this one.");
		}

		if (candidate.IncompleteCompetitiveMatches > 0)
		{
			reasons.Add(
				$"Complete or remove {candidate.IncompleteCompetitiveMatches} outstanding competitive match(es).");
		}

		if (candidate.CompletedMatches.Count == 0)
		{
			reasons.Add("There are no completed competitive matches to roll over.");
		}

		return reasons;
	}

	private static SeasonRolloverPreviewViewModel ToPreview(RolloverCandidate candidate)
	{
		var rollover = candidate.ExistingRollover;
		var players = rollover?.Players ?? candidate.Players;
		var blockingReasons = rollover is null ? GetBlockingReasons(candidate) : [];

		return new SeasonRolloverPreviewViewModel
		{
			SeasonId = candidate.Season.Id,
			SeasonName = candidate.Season.Name,
			IsSeasonActive = candidate.Season.IsActive,
			IsAlreadyRolledOver = rollover is not null,
			CanRollOver = rollover is null && blockingReasons.Count == 0,
			RolledOverAt = rollover?.RolledOverAt,
			CompletedCompetitiveMatches = rollover?.CompletedCompetitiveMatches ??
				candidate.CompletedMatches.Count,
			IncompleteCompetitiveMatches = candidate.IncompleteCompetitiveMatches,
			AffectedPlayers = players.Count(player => player.SeasonApps > 0),
			AppearancesToAdd = players.Sum(player => player.SeasonApps),
			GoalsToAdd = players.Sum(player => player.SeasonGoals),
			BlockingReasons = blockingReasons,
			Players = players
				.Where(player => player.SeasonApps > 0 || player.SeasonGoals > 0)
				.Select(player => new SeasonRolloverPlayerViewModel
				{
					PlayerId = player.PlayerId,
					PlayerName = player.PlayerName,
					HistoricalAppsBefore = player.HistoricalAppsBefore,
					HistoricalGoalsBefore = player.HistoricalGoalsBefore,
					SeasonApps = player.SeasonApps,
					SeasonGoals = player.SeasonGoals,
					CareerAppsAfter = player.CareerAppsAfter,
					CareerGoalsAfter = player.CareerGoalsAfter
				})
				.ToList()
		};
	}

	private static PlayerMatchContributionViewModel ToMatchContribution(
		SeasonRolloverMatchContribution match)
	{
		return new PlayerMatchContributionViewModel
		{
			MatchId = match.MatchId,
			Date = match.Date,
			TeamId = match.TeamId,
			Team = match.Team.ToString(),
			Opponent = match.Opponent,
			Competition = match.Competition,
			Venue = match.Venue.ToString(),
			HomeGoals = match.HomeGoals,
			AwayGoals = match.AwayGoals,
			AppearanceType = match.AppearanceType.ToString(),
			Appearances = match.Appearances,
			Goals = match.Goals,
			Assists = match.Assists,
			Minutes = match.Minutes,
			YellowCards = match.YellowCards,
			RedCards = match.RedCards,
			IsMotm = match.IsMotm
		};
	}

	private sealed class RolloverCandidate
	{
		public required Season Season { get; set; }
		public SeasonRollover? ExistingRollover { get; set; }
		public List<Match> CompletedMatches { get; set; } = [];
		public int IncompleteCompetitiveMatches { get; set; }
		public List<SeasonRolloverPlayerSnapshot> Players { get; set; } = [];
	}
}
