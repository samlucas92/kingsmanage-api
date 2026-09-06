namespace KingsManage.Web.Models;

public class SeasonRolloverPreviewViewModel
{
	public Guid SeasonId { get; set; }
	public string SeasonName { get; set; } = string.Empty;
	public bool IsSeasonActive { get; set; }
	public bool IsAlreadyRolledOver { get; set; }
	public bool CanRollOver { get; set; }
	public DateTime? RolledOverAt { get; set; }
	public int CompletedCompetitiveMatches { get; set; }
	public int IncompleteCompetitiveMatches { get; set; }
	public int AffectedPlayers { get; set; }
	public int AppearancesToAdd { get; set; }
	public int GoalsToAdd { get; set; }
	public List<string> BlockingReasons { get; set; } = [];
	public List<SeasonRolloverPlayerViewModel> Players { get; set; } = [];
}

public class SeasonRolloverPlayerViewModel
{
	public Guid PlayerId { get; set; }
	public string PlayerName { get; set; } = string.Empty;
	public int HistoricalAppsBefore { get; set; }
	public int HistoricalGoalsBefore { get; set; }
	public int SeasonApps { get; set; }
	public int SeasonGoals { get; set; }
	public int CareerAppsAfter { get; set; }
	public int CareerGoalsAfter { get; set; }
}

public class PlayerStatsBreakdownViewModel
{
	public Guid SeasonId { get; set; }
	public string SeasonName { get; set; } = string.Empty;
	public Guid PlayerId { get; set; }
	public string PlayerName { get; set; } = string.Empty;
	public bool IsSeasonRolledOver { get; set; }
	public DateTime? RolledOverAt { get; set; }
	public int HistoricalApps { get; set; }
	public int HistoricalGoals { get; set; }
	public int SeasonApps { get; set; }
	public int SeasonGoals { get; set; }
	public int CareerApps { get; set; }
	public int CareerGoals { get; set; }
	public List<PlayerMatchContributionViewModel> Matches { get; set; } = [];
}

public class PlayerMatchContributionViewModel
{
	public Guid MatchId { get; set; }
	public DateTime Date { get; set; }
	public Guid TeamId { get; set; }
	public string Team { get; set; } = string.Empty;
	public string Opponent { get; set; } = string.Empty;
	public string Competition { get; set; } = string.Empty;
	public string Venue { get; set; } = string.Empty;
	public int HomeGoals { get; set; }
	public int AwayGoals { get; set; }
	public string AppearanceType { get; set; } = string.Empty;
	public int Appearances { get; set; }
	public int Goals { get; set; }
	public int Assists { get; set; }
	public int Minutes { get; set; }
	public int YellowCards { get; set; }
	public int RedCards { get; set; }
	public bool IsMotm { get; set; }
}
