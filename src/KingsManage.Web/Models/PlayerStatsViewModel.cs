using KingsManage;

namespace KingsManage.Web.Models;

public class PlayerStatsViewModel
{
	public Guid PlayerId { get; set; }

	public string PlayerName { get; set; } = string.Empty;

	public bool IsActive { get; set; }

	public int FirstTeamApps { get; set; }

	public int FirstTeamGoals { get; set; }

	public int SecondTeamApps { get; set; }

	public int SecondTeamGoals { get; set; }

	public int SeasonApps { get; set; }

	public int SeasonGoals { get; set; }

	public int PreSeasonApps { get; set; }

	public int PreSeasonGoals { get; set; }

	public int TrackedCareerApps { get; set; }

	public int TrackedCareerGoals { get; set; }

	public int CareerApps { get; set; }

	public int CareerGoals { get; set; }

	public int Assists { get; set; }

	public int Starts { get; set; }

	public int Bench { get; set; }

	public int UnusedSubstitutes { get; set; }

	public int Motm { get; set; }

	public int Minutes { get; set; }

	public int YellowCards { get; set; }

	public int RedCards { get; set; }

	public List<PlayerTeamStatsViewModel> TeamStats { get; set; } = [];

	public static PlayerStatsViewModel FromStats(
		Player player,
		IReadOnlyList<PlayerSeasonStats> selectedSeasonStats,
		IReadOnlyList<PlayerSeasonStats> allSeasonStats,
		PlayerHistoricalStats? historicalStats
	)
	{
		var playerSelectedSeasonStats = selectedSeasonStats
			.Where(stats => stats.PlayerId == player.Id)
			.ToList();
		var playerAllSeasonStats = allSeasonStats
			.Where(stats => stats.PlayerId == player.Id)
			.ToList();
		var firstTeamStats = playerSelectedSeasonStats
			.Where(stats => stats.Team == ClubTeam.First)
			.ToList();
		var secondTeamStats = playerSelectedSeasonStats
			.Where(stats => stats.Team == ClubTeam.Second)
			.ToList();
		var preSeasonApps = historicalStats?.Appearances ?? 0;
		var preSeasonGoals = historicalStats?.Goals ?? 0;
		var trackedCareerApps = playerAllSeasonStats.Sum(stats => stats.Appearances);
		var trackedCareerGoals = playerAllSeasonStats.Sum(stats => stats.Goals);

		return new PlayerStatsViewModel
		{
			PlayerId = player.Id,
			PlayerName = player.Name,
			IsActive = player.IsActive,
			FirstTeamApps = firstTeamStats.Sum(stats => stats.Appearances),
			FirstTeamGoals = firstTeamStats.Sum(stats => stats.Goals),
			SecondTeamApps = secondTeamStats.Sum(stats => stats.Appearances),
			SecondTeamGoals = secondTeamStats.Sum(stats => stats.Goals),
			SeasonApps = playerSelectedSeasonStats.Sum(stats => stats.Appearances),
			SeasonGoals = playerSelectedSeasonStats.Sum(stats => stats.Goals),
			PreSeasonApps = preSeasonApps,
			PreSeasonGoals = preSeasonGoals,
			TrackedCareerApps = trackedCareerApps,
			TrackedCareerGoals = trackedCareerGoals,
			CareerApps = preSeasonApps + trackedCareerApps,
			CareerGoals = preSeasonGoals + trackedCareerGoals,
			Assists = playerSelectedSeasonStats.Sum(stats => stats.Assists),
			Starts = playerSelectedSeasonStats.Sum(stats => stats.Starts),
			Bench = playerSelectedSeasonStats.Sum(stats => stats.Bench),
			UnusedSubstitutes = playerSelectedSeasonStats.Sum(stats => stats.UnusedSubstitutes),
			Motm = playerSelectedSeasonStats.Sum(stats => stats.Motm),
			Minutes = playerSelectedSeasonStats.Sum(stats => stats.Minutes),
			YellowCards = playerSelectedSeasonStats.Sum(stats => stats.YellowCards),
			RedCards = playerSelectedSeasonStats.Sum(stats => stats.RedCards),
			TeamStats = playerSelectedSeasonStats
				.GroupBy(stats => stats.TeamId ?? DefaultClubTeams.FromLegacy(stats.Team))
				.Select(group => new PlayerTeamStatsViewModel
				{
					TeamId = group.Key,
					Appearances = group.Sum(stats => stats.Appearances),
					Goals = group.Sum(stats => stats.Goals),
					Assists = group.Sum(stats => stats.Assists),
					Minutes = group.Sum(stats => stats.Minutes)
				})
				.ToList()
		};
	}
}

public class PlayerTeamStatsViewModel
{
	public Guid TeamId { get; set; }
	public int Appearances { get; set; }
	public int Goals { get; set; }
	public int Assists { get; set; }
	public int Minutes { get; set; }
}
