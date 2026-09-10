using KingsManage.Web.Models;

namespace KingsManage.Web.Services;

public interface ILeagueEligibilityService
{
	Task<MatchEligibilityModel> EvaluateAsync(
		Match match,
		IReadOnlyCollection<Guid> selectedPlayerIds,
		CancellationToken cancellationToken = default);
}

public sealed class LeagueEligibilityService : ILeagueEligibilityService
{
	private readonly ILeagueRuleService ruleService;
	private readonly IMatchService matchService;

	public LeagueEligibilityService(ILeagueRuleService ruleService, IMatchService matchService)
	{
		this.ruleService = ruleService;
		this.matchService = matchService;
	}

	public async Task<MatchEligibilityModel> EvaluateAsync(
		Match match,
		IReadOnlyCollection<Guid> selectedPlayerIds,
		CancellationToken cancellationToken = default)
	{
		var result = new MatchEligibilityModel();
		var targetTeamId = ResolveTeamId(match);
		var rules = (await ruleService.GetAllAsync(cancellationToken))
			.Where(rule => rule.IsActive && rule.RestrictedTeamId == targetTeamId)
			.Where(rule => MatchesCompetition(match.Competition, rule.RestrictedTeamCompetitions))
			.ToList();
		if (rules.Count == 0) return result;

		var matches = match.SeasonId.HasValue
			? await matchService.GetBySeasonAsync(match.SeasonId.Value, cancellationToken)
			: await matchService.GetAllAsync(cancellationToken);
		var selected = selectedPlayerIds.ToHashSet();

		foreach (var rule in rules)
		{
			var evaluation = rule.RuleType == LeagueRuleType.CupTied
				? EvaluateCupTied(rule, match, matches, selected)
				: EvaluateRecentAppearance(rule, match, matches, selected);
			result.Rules.Add(evaluation);
			if (!evaluation.IsExempt && rule.RuleType == LeagueRuleType.CupTied)
			{
				var selectedCupTied = evaluation.AffectedPlayerIds.Count(selected.Contains);
				if (selectedCupTied > 0)
					result.Violations.Add($"{selectedCupTied} selected player{(selectedCupTied == 1 ? " is" : "s are")} cup-tied under {rule.Name}.");
			}
			else if (!evaluation.IsExempt && evaluation.MaxPlayers.HasValue && evaluation.SelectedCount > evaluation.MaxPlayers.Value)
			{
				result.Violations.Add($"{rule.Name} allows {evaluation.MaxPlayers.Value}, but {evaluation.SelectedCount} affected players are selected.");
			}
		}

		result.IsValid = result.Violations.Count == 0;
		return result;
	}

	private static LeagueRuleEvaluationModel EvaluateRecentAppearance(
		LeagueRule rule, Match target, IReadOnlyList<Match> matches, HashSet<Guid> selected)
	{
		var higherMatches = matches.Where(item => item.Id != target.Id && ResolveTeamId(item) == rule.HigherTeamId);
		var isExempt = rule.ExemptWhenHigherTeamPlaysSameDay && higherMatches.Any(item =>
			item.State != MatchState.Postponed && item.Date.Date == target.Date.Date);
		var previous = higherMatches
			.Where(item => item.State != MatchState.Postponed && item.Date < target.Date)
			.Where(item => MatchesCompetition(item.Competition, rule.HigherTeamCompetitions))
			.Where(item => item.IsCompleted || item.IsLineupLocked || item.SelectedPlayers.Count > 0)
			.OrderByDescending(item => item.Date)
			.FirstOrDefault();
		var affected = previous is null ? [] : GetSquadPlayerIds(previous);
		var selectedCount = affected.Count(selected.Contains);
		return new LeagueRuleEvaluationModel
		{
			RuleId = rule.Id,
			Name = rule.Name,
			RuleType = rule.RuleType,
			IsExempt = isExempt,
			MaxPlayers = rule.MaxPlayers,
			SelectedCount = selectedCount,
			AffectedPlayerIds = affected,
			Summary = isExempt
				? $"Exempt because the higher team also plays on {target.Date:dd MMM}."
				: previous is null
					? "No previous qualifying higher-team match was found."
					: previous.IsCompleted
						? $"{selectedCount}/{rule.MaxPlayers ?? 0} players selected from the higher team's {previous.Date:dd MMM} match."
						: $"{selectedCount}/{rule.MaxPlayers ?? 0} players selected from the higher team's saved {previous.Date:dd MMM} lineup."
		};
	}

	private static LeagueRuleEvaluationModel EvaluateCupTied(
		LeagueRule rule, Match target, IReadOnlyList<Match> matches, HashSet<Guid> selected)
	{
		var affected = matches
			.Where(item => item.Id != target.Id && ResolveTeamId(item) == rule.HigherTeamId)
			.Where(item => item.IsCompleted && item.Date < target.Date)
			.Where(item => MatchesCompetition(item.Competition, rule.HigherTeamCompetitions))
			.SelectMany(GetSquadPlayerIds)
			.Distinct()
			.ToList();
		var selectedCount = affected.Count(selected.Contains);
		return new LeagueRuleEvaluationModel
		{
			RuleId = rule.Id,
			Name = rule.Name,
			RuleType = rule.RuleType,
			SelectedCount = selectedCount,
			AffectedPlayerIds = affected,
			Summary = affected.Count == 0
				? "No players are currently cup-tied by this rule."
				: $"{affected.Count} player{(affected.Count == 1 ? " is" : "s are")} cup-tied; {selectedCount} selected."
		};
	}

	private static List<Guid> GetSquadPlayerIds(Match match)
	{
		var recorded = match.PlayerStats
			.Where(stats => stats.AppearanceType is
				MatchAppearanceType.Started or
				MatchAppearanceType.SubstituteUsed or
				MatchAppearanceType.UnusedSubstitute)
			.Select(stats => stats.PlayerId);

		return recorded
			.Concat(match.SelectedPlayers.Select(player => player.PlayerId))
			.Distinct()
			.ToList();
	}

	private static Guid ResolveTeamId(Match match) => match.TeamId ?? DefaultClubTeams.FromLegacy(match.Team);

	private static bool MatchesCompetition(string competition, IReadOnlyCollection<string> configured)
	{
		if (configured.Count == 0) return true;
		var competitionType = MatchCompetition.GetCompetitionType(competition);
		return configured.Any(value =>
			string.Equals(value, competition, StringComparison.OrdinalIgnoreCase) ||
			TryGetCompetitionCategory(value, out var category) && category == competitionType);
	}

	private static bool TryGetCompetitionCategory(string value, out MatchCompetitionType category)
	{
		if (Enum.TryParse(value.Trim(), true, out category) && category != MatchCompetitionType.Unknown)
			return true;
		category = MatchCompetitionType.Unknown;
		return false;
	}
}
