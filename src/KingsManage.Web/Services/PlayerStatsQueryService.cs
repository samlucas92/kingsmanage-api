using KingsManage.Web.Models;

namespace KingsManage.Web.Services;

public sealed class PlayerStatsQueryService : IPlayerStatsQueryService
{
	private readonly IMatchService matchService;
	private readonly IPlayerService playerService;
	private readonly ISeasonRolloverStore? rolloverStore;
	private readonly IStatsService statsService;

	public PlayerStatsQueryService(
		IMatchService matchService,
		IPlayerService playerService,
		IStatsService statsService,
		ISeasonRolloverStore? rolloverStore = null)
	{
		this.matchService = matchService;
		this.playerService = playerService;
		this.statsService = statsService;
		this.rolloverStore = rolloverStore;
	}

	public async Task<List<PlayerStatsViewModel>> BuildRowsAsync(
		Guid seasonId,
		bool includeFriendlies = true,
		CancellationToken cancellationToken = default)
	{
		var rollover = rolloverStore is null
			? null
			: await rolloverStore.GetBySeasonIdAsync(seasonId, cancellationToken);
		if (rollover is not null)
		{
			return rollover.Players
				.OrderBy(player => player.PlayerName)
				.Select(PlayerStatsViewModel.FromRollover)
				.ToList();
		}

		var players = await playerService.GetAllAsync(cancellationToken);
		var selectedSeasonStats = includeFriendlies
			? await statsService.GetSeasonStatsAsync(seasonId, cancellationToken)
			: await CalculateCompetitiveStatsAsync(seasonId, cancellationToken);
		var historicalStats = await statsService.GetHistoricalStatsAsync(cancellationToken);
		var historicalStatsByPlayerId = historicalStats
			.GroupBy(stats => stats.PlayerId)
			.ToDictionary(group => group.Key, group => group.First());

		return players
			.OrderBy(player => player.Name)
			.Select(player => PlayerStatsViewModel.FromStats(
				player,
				selectedSeasonStats,
				historicalStatsByPlayerId.GetValueOrDefault(player.Id)))
			.ToList();
	}

	private async Task<List<PlayerSeasonStats>> CalculateCompetitiveStatsAsync(
		Guid seasonId,
		CancellationToken cancellationToken)
	{
		var allMatches = await matchService.GetAllAsync(cancellationToken);
		var competitiveMatches = allMatches
			.Where(match => !MatchCompetition.IsFriendly(match.Competition))
			.ToList();
		return SeasonStatsCalculator.Calculate(
			seasonId,
			competitiveMatches);
	}
}
