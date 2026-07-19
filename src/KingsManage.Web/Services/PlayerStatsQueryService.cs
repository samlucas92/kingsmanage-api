using KingsManage.Web.Models;

namespace KingsManage.Web.Services;

public sealed class PlayerStatsQueryService : IPlayerStatsQueryService
{
	private readonly IMatchService matchService;
	private readonly IPlayerService playerService;
	private readonly IStatsService statsService;

	public PlayerStatsQueryService(
		IMatchService matchService,
		IPlayerService playerService,
		IStatsService statsService)
	{
		this.matchService = matchService;
		this.playerService = playerService;
		this.statsService = statsService;
	}

	public async Task<List<PlayerStatsViewModel>> BuildRowsAsync(
		Guid seasonId,
		bool includeFriendlies = true,
		CancellationToken cancellationToken = default)
	{
		var players = await playerService.GetAllAsync(cancellationToken);
		var (selectedSeasonStats, allSeasonStats) = includeFriendlies
			? await GetStoredStatsAsync(seasonId, cancellationToken)
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
				allSeasonStats,
				historicalStatsByPlayerId.GetValueOrDefault(player.Id)))
			.ToList();
	}

	private async Task<(List<PlayerSeasonStats> SelectedSeasonStats, List<PlayerSeasonStats> AllSeasonStats)> GetStoredStatsAsync(
		Guid seasonId,
		CancellationToken cancellationToken)
	{
		var selectedSeasonStats = await statsService.GetSeasonStatsAsync(
			seasonId,
			cancellationToken);
		var allSeasonStats = await statsService.GetAllSeasonStatsAsync(cancellationToken);

		return (selectedSeasonStats, allSeasonStats);
	}

	private async Task<(List<PlayerSeasonStats> SelectedSeasonStats, List<PlayerSeasonStats> AllSeasonStats)> CalculateCompetitiveStatsAsync(
		Guid seasonId,
		CancellationToken cancellationToken)
	{
		var allMatches = await matchService.GetAllAsync(cancellationToken);
		var competitiveMatches = allMatches
			.Where(match => !MatchCompetition.IsFriendly(match.Competition))
			.ToList();
		var selectedSeasonStats = SeasonStatsCalculator.Calculate(
			seasonId,
			competitiveMatches);
		var allSeasonStats = competitiveMatches
			.Where(match => match.SeasonId is not null)
			.Select(match => match.SeasonId!.Value)
			.Distinct()
			.SelectMany(matchSeasonId => SeasonStatsCalculator.Calculate(
				matchSeasonId,
				competitiveMatches))
			.ToList();

		return (selectedSeasonStats, allSeasonStats);
	}
}
