using KingsManage;
using KingsManage.Web.Models;

namespace KingsManage.Web.Services;

public sealed class PlayerReportsQueryService : IPlayerReportsQueryService
{
	private readonly IClubFormService formService;
	private readonly IMatchService matchService;
	private readonly IPlayerService playerService;
	private readonly IPlayerStatsQueryService playerStatsQueryService;

	public PlayerReportsQueryService(
		IClubFormService formService,
		IMatchService matchService,
		IPlayerService playerService,
		IPlayerStatsQueryService playerStatsQueryService)
	{
		this.formService = formService;
		this.matchService = matchService;
		this.playerService = playerService;
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
		var awards = await BuildAwardsReportAsync(
			seasonId,
			teamId,
			playerId,
			includeFriendlies,
			cancellationToken);

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
			Awards = awards,
			Discipline = BuildDisciplineReport(rows)
		};
	}

	public async Task<List<PlayerContributionViewModel>> GetTopContributorsAsync(
		Guid seasonId,
		int limit,
		bool includeFriendlies = true,
		CancellationToken cancellationToken = default)
	{
		var rows = await playerStatsQueryService.BuildRowsAsync(
			seasonId,
			includeFriendlies,
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

	private async Task<PlayerAwardsReportViewModel> BuildAwardsReportAsync(
		Guid seasonId,
		Guid? teamId,
		Guid? playerId,
		bool includeFriendlies,
		CancellationToken cancellationToken)
	{
		var forms = await formService.GetAllAsync(cancellationToken);
		var awardForms = forms
			.Where(form =>
				form.Status == ClubFormStatus.Closed &&
				form.SourceMatchId.HasValue &&
				(form.FormType == ClubFormType.PlayerOfTheMatch ||
					form.SourceType == ClubFormSourceType.MatchAwards))
			.ToList();
		if (awardForms.Count == 0)
		{
			return new PlayerAwardsReportViewModel();
		}

		var matches = await matchService.GetBySeasonAsync(seasonId, cancellationToken);
		var matchLookup = matches
			.Where(match =>
				(includeFriendlies || !MatchCompetition.IsFriendly(match.Competition)) &&
				(teamId is null || (match.TeamId ?? DefaultClubTeams.FromLegacy(match.Team)) == teamId.Value))
			.ToDictionary(match => match.Id);
		var players = await playerService.GetAllAsync(cancellationToken);
		var playerLookup = players.ToDictionary(player => player.Id, player => player.Name);
		var manOfTheMatchCounts = new Dictionary<Guid, int>();
		var dickOfTheDayCounts = new Dictionary<Guid, int>();

		foreach (var form in awardForms)
		{
			if (!matchLookup.ContainsKey(form.SourceMatchId!.Value))
			{
				continue;
			}

			var submissions = await formService.GetSubmissionsAsync(form.Id, cancellationToken);
			CountTopQuestionAnswers(
				form,
				submissions,
				"Man of the match",
				players,
				manOfTheMatchCounts);
			CountTopQuestionAnswers(
				form,
				submissions,
				"Dick of the day",
				players,
				dickOfTheDayCounts);
		}

		return new PlayerAwardsReportViewModel
		{
			ManOfTheMatch = BuildAwardRows(manOfTheMatchCounts, playerLookup, playerId),
			DickOfTheDay = BuildAwardRows(dickOfTheDayCounts, playerLookup, playerId)
		};
	}

	private static void CountTopQuestionAnswers(
		ClubForm form,
		IReadOnlyList<ClubFormSubmission> submissions,
		string questionPrompt,
		IReadOnlyList<Player> players,
		Dictionary<Guid, int> counts)
	{
		foreach (var answer in GetTopChoiceAnswers(form, submissions, questionPrompt))
		{
			var playerId = ResolvePlayerIdFromChoice(form, questionPrompt, answer, players);
			if (!playerId.HasValue)
			{
				continue;
			}

			counts[playerId.Value] = counts.GetValueOrDefault(playerId.Value) + 1;
		}
	}

	private static List<PlayerAwardCountViewModel> BuildAwardRows(
		Dictionary<Guid, int> counts,
		IReadOnlyDictionary<Guid, string> playerLookup,
		Guid? playerId)
	{
		return counts
			.Where(item => playerId is null || item.Key == playerId.Value)
			.Select(item => new PlayerAwardCountViewModel
			{
				PlayerId = item.Key,
				PlayerName = playerLookup.GetValueOrDefault(item.Key) ?? "Unknown player",
				Count = item.Value
			})
			.OrderByDescending(row => row.Count)
			.ThenBy(row => row.PlayerName)
			.ToList();
	}

	private static List<string> GetTopChoiceAnswers(
		ClubForm form,
		IReadOnlyList<ClubFormSubmission> submissions,
		string questionPrompt)
	{
		var question = form.Questions.FirstOrDefault(question =>
			string.Equals(question.Prompt, questionPrompt, StringComparison.OrdinalIgnoreCase));
		if (question is null)
		{
			return [];
		}

		var rankedAnswers = submissions
			.SelectMany(submission => submission.Answers)
			.Where(answer => answer.QuestionId == question.Id)
			.SelectMany(answer => answer.SelectedOptions)
			.Where(option => !string.IsNullOrWhiteSpace(option))
			.GroupBy(option => option, StringComparer.OrdinalIgnoreCase)
			.Select(group => new { Value = group.Key, Count = group.Count() })
			.OrderByDescending(result => result.Count)
			.ThenBy(result => result.Value)
			.ToList();

		if (rankedAnswers.Count == 0)
		{
			return [];
		}

		var winningCount = rankedAnswers[0].Count;
		return rankedAnswers
			.Where(result => result.Count == winningCount)
			.Select(result => result.Value)
			.ToList();
	}

	private static Guid? ResolvePlayerIdFromChoice(
		ClubForm form,
		string questionPrompt,
		string selectedValue,
		IReadOnlyList<Player> players)
	{
		var question = form.Questions.FirstOrDefault(question =>
			string.Equals(question.Prompt, questionPrompt, StringComparison.OrdinalIgnoreCase));
		var choice = question is null
			? null
			: GetChoiceOptions(question).FirstOrDefault(option =>
				string.Equals(option.Value, selectedValue, StringComparison.OrdinalIgnoreCase) ||
				string.Equals(option.Label, selectedValue, StringComparison.OrdinalIgnoreCase));

		if (choice?.PlayerId is not null)
		{
			return choice.PlayerId.Value;
		}

		var resolution = (form.AwardResolutions ?? []).FirstOrDefault(resolution =>
			string.Equals(resolution.QuestionPrompt, questionPrompt, StringComparison.OrdinalIgnoreCase) &&
			string.Equals(resolution.SelectedValue, selectedValue, StringComparison.OrdinalIgnoreCase));
		if (resolution is not null && resolution.PlayerId != Guid.Empty)
		{
			return resolution.PlayerId;
		}

		if (Guid.TryParse(selectedValue, out var selectedPlayerId))
		{
			return selectedPlayerId;
		}

		return players.FirstOrDefault(player =>
			string.Equals(player.Name, selectedValue, StringComparison.OrdinalIgnoreCase))?.Id;
	}

	private static List<ClubFormQuestionOption> GetChoiceOptions(ClubFormQuestion question)
	{
		if (question.ChoiceOptions?.Count > 0)
		{
			return question.ChoiceOptions
				.Where(option => !string.IsNullOrWhiteSpace(option.Value) || !string.IsNullOrWhiteSpace(option.Label))
				.Select(option => new ClubFormQuestionOption
				{
					Value = string.IsNullOrWhiteSpace(option.Value)
						? option.Label
						: option.Value,
					Label = string.IsNullOrWhiteSpace(option.Label)
						? option.Value
						: option.Label,
					PlayerId = option.PlayerId
				})
				.ToList();
		}

		return (question.Options ?? [])
			.Where(option => !string.IsNullOrWhiteSpace(option))
			.Select(option => new ClubFormQuestionOption
			{
				Value = option,
				Label = option
			})
			.ToList();
	}
}
