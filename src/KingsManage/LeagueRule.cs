namespace KingsManage;

public enum LeagueRuleType
{
	RecentHigherTeamAppearanceLimit,
	CupTied
}

public sealed class LeagueRule : ITenantOwned
{
	public Guid OrganizationId { get; set; }
	public Guid ClubId { get; set; }
	public Guid Id { get; set; } = Guid.NewGuid();
	public string Name { get; set; } = string.Empty;
	public bool IsActive { get; set; } = true;
	public LeagueRuleType RuleType { get; set; }
	public Guid HigherTeamId { get; set; }
	public Guid RestrictedTeamId { get; set; }
	public int? MaxPlayers { get; set; }
	public bool ExemptWhenHigherTeamPlaysSameDay { get; set; }
	public List<string> HigherTeamCompetitions { get; set; } = [];
	public List<string> RestrictedTeamCompetitions { get; set; } = [];
	public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
	public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
