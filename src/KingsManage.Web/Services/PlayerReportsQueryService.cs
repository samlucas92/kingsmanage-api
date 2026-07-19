using KingsManage.Web.Models;

namespace KingsManage.Web.Services;

public sealed class PlayerReportsQueryService : IPlayerReportsQueryService
{
	private readonly IPlayerStatsQueryService playerStatsQueryService;

	public PlayerReportsQueryService(IPlayerStatsQueryService playerStatsQueryService)
	{
		this.playerStatsQueryService = playerStatsQueryService;
	}

	public async Task<PlayerReportsViewModel> GetAsync(
		Guid seasonId,
		Guid? teamId,
		Guid? playerId,
		bool includeFriendlies = true,
		CancellationToken cancellationToken = default)
	{
		var rows = await playerStatsQueryService.BuildRowsAsync(
			seasonId,
			includeFriendlies,
			cancellationToken);

		rows = FilterRows(rows, teamId, playerId);
		var activeRows = rows.Where(row => row.IsActive).ToList();

		return new PlayerReportsViewModel
		{
			Summary = new PlayerStatsSummaryViewModel
			{
				ActivePlayers = activeRows.Count,
				Appearances = activeRows.Sum(row => row.SeasonApps),
				Goals = activeRows.Sum(row => row.SeasonGoals),
				Assists = activeRows.Sum(row => row.Assists),
				Contributions = activeRows.Sum(row => row.SeasonGoals + row.Assists),
				Minutes = activeRows.Sum(row => row.Minutes)
			},
			Players = rows,
			TopContributors = BuildTopContributors(activeRows, 10),
			SquadUsage = BuildSquadUsage(activeRows, teamId),
			Discipline = BuildDisciplineReport(rows)
		};
	}

	public async Task<List<PlayerContributionViewModel>> GetTopContributorsAsync(
		Guid seasonId,
		int limit,
		CancellationToken cancellationToken = default)
	{
		var rows = await playerStatsQueryService.BuildRowsAsync(
			seasonId,
			includeFriendlies: true,
			cancellationToken);

		return BuildTopContributors(rows, limit);
	}

	private static List<PlayerStatsViewModel> FilterRows(
		IEnumerable<PlayerStatsViewModel> rows,
		Guid? teamId,
		Guid? playerId)
	{
		return rows
			.Where(row => playerId is null || row.PlayerId == playerId.Value)
			.Where(row =>
				teamId is null ||
				row.TeamStats.Any(stats => stats.TeamId == teamId.Value))
			.ToList();
	}

	private static List<PlayerContributionViewModel> BuildTopContributors(
		IEnumerable<PlayerStatsViewModel> rows,
		int limit)
	{
		return rows
			.Where(row => row.IsActive)
			.Select(row => new PlayerContributionViewModel
			{
				PlayerId = row.PlayerId,
				PlayerName = row.PlayerName,
				Goals = row.SeasonGoals,
				Assists = row.Assists,
				Contributions = row.SeasonGoals + row.Assists,
				Appearances = row.SeasonApps
			})
			.Where(row => row.Contributions > 0 || row.Appearances > 0)
			.OrderByDescending(row => row.Contributions)
			.ThenByDescending(row => row.Appearances)
			.ThenBy(row => row.PlayerName)
			.Take(limit)
			.ToList();
	}

	private static List<PlayerUsageViewModel> BuildSquadUsage(
		IEnumerable<PlayerStatsViewModel> rows,
		Guid? teamId)
	{
		return rows
			.Select(row =>
			{
				var teamStats = teamId is null
					? null
					: row.TeamStats.FirstOrDefault(stats => stats.TeamId == teamId.Value);

				return new PlayerUsageViewModel
				{
					PlayerId = row.PlayerId,
					PlayerName = row.PlayerName,
					Appearances = teamStats?.Appearances ?? row.SeasonApps,
					Starts = row.Starts,
					Bench = row.Bench,
					UnusedSubstitutes = row.UnusedSubstitutes,
					Minutes = teamStats?.Minutes ?? row.Minutes,
					Goals = teamStats?.Goals ?? row.SeasonGoals,
					Assists = teamStats?.Assists ?? row.Assists
				};
			})
			.Where(row =>
				row.Appearances > 0 ||
				row.Starts > 0 ||
				row.Bench > 0 ||
				row.UnusedSubstitutes > 0 ||
				row.Minutes > 0)
			.OrderByDescending(row => row.Minutes)
			.ThenByDescending(row => row.Appearances)
			.ThenBy(row => row.PlayerName)
			.ToList();
	}

	private static DisciplineReportViewModel BuildDisciplineReport(
		IEnumerable<PlayerStatsViewModel> rows)
	{
		var playerRows = rows
			.Select(row => new PlayerDisciplineViewModel
			{
				PlayerId = row.PlayerId,
				PlayerName = row.PlayerName,
				YellowCards = row.YellowCards,
				RedCards = row.RedCards,
				TotalCards = row.YellowCards + row.RedCards
			})
			.Where(row => row.TotalCards > 0)
			.OrderByDescending(row => row.TotalCards)
			.ThenByDescending(row => row.RedCards)
			.ThenBy(row => row.PlayerName)
			.ToList();

		return new DisciplineReportViewModel
		{
			YellowCards = playerRows.Sum(row => row.YellowCards),
			RedCards = playerRows.Sum(row => row.RedCards),
			TotalCards = playerRows.Sum(row => row.TotalCards),
			Players = playerRows
		};
	}
}
