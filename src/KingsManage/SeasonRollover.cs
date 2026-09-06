namespace KingsManage;

public class SeasonRollover : ITenantOwned
{
	public Guid OrganizationId { get; set; }
	public Guid ClubId { get; set; }
	public Guid Id { get; set; } = Guid.NewGuid();
	public Guid SeasonId { get; set; }
	public string SeasonName { get; set; } = string.Empty;
	public int CompletedCompetitiveMatches { get; set; }
	public List<SeasonRolloverPlayerSnapshot> Players { get; set; } = [];
	public DateTime RolledOverAt { get; set; } = DateTime.UtcNow;
}

public class SeasonRolloverPlayerSnapshot
{
	public Guid PlayerId { get; set; }
	public string PlayerName { get; set; } = string.Empty;
	public bool IsActive { get; set; }
	public int HistoricalAppsBefore { get; set; }
	public int HistoricalGoalsBefore { get; set; }
	public int SeasonApps { get; set; }
	public int SeasonGoals { get; set; }
	public int CareerAppsAfter { get; set; }
	public int CareerGoalsAfter { get; set; }
	public int Starts { get; set; }
	public int Bench { get; set; }
	public int UnusedSubstitutes { get; set; }
	public int Assists { get; set; }
	public int Minutes { get; set; }
	public int Motm { get; set; }
	public int YellowCards { get; set; }
	public int RedCards { get; set; }
	public List<SeasonRolloverTeamSnapshot> TeamStats { get; set; } = [];
	public List<SeasonRolloverMatchContribution> Matches { get; set; } = [];
}

public class SeasonRolloverTeamSnapshot
{
	public Guid TeamId { get; set; }
	public ClubTeam Team { get; set; }
	public int Appearances { get; set; }
	public int Goals { get; set; }
	public int Assists { get; set; }
	public int Minutes { get; set; }
}

public class SeasonRolloverMatchContribution
{
	public Guid MatchId { get; set; }
	public DateTime Date { get; set; }
	public Guid TeamId { get; set; }
	public ClubTeam Team { get; set; }
	public string Opponent { get; set; } = string.Empty;
	public string Competition { get; set; } = string.Empty;
	public MatchVenue Venue { get; set; }
	public int HomeGoals { get; set; }
	public int AwayGoals { get; set; }
	public MatchAppearanceType AppearanceType { get; set; }
	public int Appearances { get; set; }
	public int Goals { get; set; }
	public int Assists { get; set; }
	public int Minutes { get; set; }
	public int YellowCards { get; set; }
	public int RedCards { get; set; }
	public bool IsMotm { get; set; }
}
