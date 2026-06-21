namespace KingsManage;

public class Match : ITenantOwned
{
	public Guid OrganizationId { get; set; }
	public Guid ClubId { get; set; }
	public Guid Id { get; set; }
	public Guid? SeasonId { get; set; }
	public Guid? ClubEventId { get; set; }
	public Guid? TeamId { get; set; }
	public ClubTeam Team { get; set; }
	public string Opponent { get; set; } = string.Empty;
	public string Competition { get; set; } = string.Empty;
	public DateTime Date { get; set; }
	public MatchVenue Venue { get; set; }
	public string Location { get; set; } = string.Empty;
	public MatchState State { get; set; } = MatchState.Upcoming;
	public MatchResult? Result { get; set; }
	public bool IsCompleted { get; set; }
	public bool IsLineupLocked { get; set; }
	public LineupFormation SelectedFormation { get; set; } = LineupFormation.FourThreeThree;
	public string FormationKey { get; set; } = string.Empty;
	public MatchNotes? Notes { get; set; }
	public List<PostponementAudit> Postponements { get; set; } = [];
	public List<SelectedPlayer> SelectedPlayers { get; set; } = [];
	public List<MatchPlayerStats> PlayerStats { get; set; } = [];
	public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
	public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
