using KingsManage;

namespace KingsManage.Web.Models;

public sealed class SaveLeagueRuleModel
{
	public string Name { get; set; } = string.Empty;
	public bool IsActive { get; set; } = true;
	public LeagueRuleType RuleType { get; set; }
	public Guid HigherTeamId { get; set; }
	public Guid RestrictedTeamId { get; set; }
	public int? MaxPlayers { get; set; }
	public bool ExemptWhenHigherTeamPlaysSameDay { get; set; }
	public List<string> HigherTeamCompetitions { get; set; } = [];
	public List<string> RestrictedTeamCompetitions { get; set; } = [];

	public LeagueRule ToRule(Guid id = default) => new()
	{
		Id = id,
		Name = Name,
		IsActive = IsActive,
		RuleType = RuleType,
		HigherTeamId = HigherTeamId,
		RestrictedTeamId = RestrictedTeamId,
		MaxPlayers = MaxPlayers,
		ExemptWhenHigherTeamPlaysSameDay = ExemptWhenHigherTeamPlaysSameDay,
		HigherTeamCompetitions = HigherTeamCompetitions ?? [],
		RestrictedTeamCompetitions = RestrictedTeamCompetitions ?? []
	};
}

public sealed class MatchEligibilityModel
{
	public bool IsValid { get; set; } = true;
	public List<string> Violations { get; set; } = [];
	public List<LeagueRuleEvaluationModel> Rules { get; set; } = [];
}

public sealed class LeagueRuleEvaluationModel
{
	public Guid RuleId { get; set; }
	public string Name { get; set; } = string.Empty;
	public LeagueRuleType RuleType { get; set; }
	public bool IsExempt { get; set; }
	public int? MaxPlayers { get; set; }
	public int SelectedCount { get; set; }
	public List<Guid> AffectedPlayerIds { get; set; } = [];
	public string Summary { get; set; } = string.Empty;
}
