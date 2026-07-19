using KingsManage.Web.Models;

namespace KingsManage.Web.Services;

public sealed class TeamPerformanceReportQueryService : ITeamPerformanceReportQueryService
{
	private readonly IMatchService matchService;

	public TeamPerformanceReportQueryService(IMatchService matchService)
	{
		this.matchService = matchService;
	}

	public async Task<TeamPerformanceReportViewModel> GetAsync(
		ReportFilters filters,
		CancellationToken cancellationToken = default)
	{
		var completedMatches = await GetFilteredCompletedMatchesAsync(
			filters,
			cancellationToken);

		return BuildReport(completedMatches);
	}

	private async Task<List<Match>> GetFilteredCompletedMatchesAsync(
		ReportFilters filters,
		CancellationToken cancellationToken)
	{
		var matches = await matchService.GetBySeasonAsync(
			filters.SeasonId,
			cancellationToken);

		return matches
			.Where(match =>
				match.IsCompleted &&
				match.State != MatchState.Postponed &&
				match.Result is not null &&
				(filters.IncludeFriendlies || !MatchCompetition.IsFriendly(match.Competition)) &&
				(filters.TeamId is null || (match.TeamId ?? DefaultClubTeams.FromLegacy(match.Team)) == filters.TeamId.Value) &&
				MatchesCompetition(match, filters.Competition) &&
				(filters.Venue is null || match.Venue == filters.Venue.Value) &&
				(filters.DateFrom is null || match.Date >= filters.DateFrom.Value.Date) &&
				(filters.DateTo is null || match.Date <= filters.DateTo.Value.Date.AddDays(1).AddTicks(-1)))
			.OrderBy(match => match.Date)
			.ToList();
	}

	private static bool MatchesCompetition(
		Match match,
		string? competition)
	{
		if (string.IsNullOrWhiteSpace(competition) ||
			competition.Equals("all", StringComparison.OrdinalIgnoreCase))
		{
			return true;
		}

		return MatchCompetition.DisplayName(match.Competition)
			.Equals(competition, StringComparison.OrdinalIgnoreCase);
	}

	private static TeamPerformanceReportViewModel BuildReport(
		IReadOnlyList<Match> completedMatches)
	{
		return new TeamPerformanceReportViewModel
		{
			Summary = BuildResultBreakdown(completedMatches),
			HomeAway = new HomeAwayBreakdownViewModel
			{
				Home = BuildResultBreakdown(completedMatches.Where(match => match.Venue == MatchVenue.Home)),
				Away = BuildResultBreakdown(completedMatches.Where(match => match.Venue == MatchVenue.Away))
			},
			Months = completedMatches
				.GroupBy(match => ReportDate.MonthStart(match.Date))
				.OrderBy(group => group.Key)
				.Select(group =>
				{
					var monthMatches = group.ToList();
					var monthBreakdown = BuildResultBreakdown(monthMatches);

					return new MonthlyResultBreakdownViewModel
					{
						Label = group.Key.ToString("MMM"),
						MonthStart = group.Key,
						Wins = monthBreakdown.Won,
						Draws = monthBreakdown.Drawn,
						Losses = monthBreakdown.Lost,
						GoalsFor = monthBreakdown.GoalsFor,
						GoalsAgainst = monthBreakdown.GoalsAgainst
					};
				})
				.ToList(),
			RecentForm = completedMatches
				.TakeLast(10)
				.Select(match =>
				{
					var goals = GetClubGoals(match);

					if (goals.For > goals.Against) return "W";
					return goals.For == goals.Against ? "D" : "L";
				})
				.ToList(),
			CleanSheets = completedMatches.Count(match => GetClubGoals(match).Against == 0),
			FailedToScore = completedMatches.Count(match => GetClubGoals(match).For == 0),
			BiggestWin = completedMatches
				.Select(BuildMatchHighlight)
				.Where(match => match.Margin > 0)
				.OrderByDescending(match => match.Margin)
				.ThenByDescending(match => match.GoalsFor)
				.FirstOrDefault(),
			BiggestLoss = completedMatches
				.Select(BuildMatchHighlight)
				.Where(match => match.Margin < 0)
				.OrderBy(match => match.Margin)
				.ThenByDescending(match => match.GoalsAgainst)
				.FirstOrDefault(),
			Competitions = completedMatches
				.GroupBy(match => MatchCompetition.DisplayName(match.Competition))
				.Select(group => new CompetitionBreakdownViewModel
				{
					Competition = group.Key,
					Summary = BuildResultBreakdown(group)
				})
				.OrderByDescending(item => item.Summary.Played)
				.ThenBy(item => item.Competition)
				.ToList()
		};
	}

	private static MatchHighlightViewModel BuildMatchHighlight(Match match)
	{
		var goals = GetClubGoals(match);

		return new MatchHighlightViewModel
		{
			MatchId = match.Id,
			Opponent = match.Opponent,
			Date = match.Date,
			GoalsFor = goals.For,
			GoalsAgainst = goals.Against,
			Margin = goals.For - goals.Against
		};
	}

	private static ResultBreakdownViewModel BuildResultBreakdown(
		IEnumerable<Match> matches)
	{
		var played = 0;
		var won = 0;
		var drawn = 0;
		var lost = 0;
		var goalsFor = 0;
		var goalsAgainst = 0;

		foreach (var match in matches.Where(match => match.Result is not null))
		{
			var goals = GetClubGoals(match);
			played += 1;
			goalsFor += goals.For;
			goalsAgainst += goals.Against;

			if (goals.For > goals.Against)
			{
				won += 1;
			}
			else if (goals.For == goals.Against)
			{
				drawn += 1;
			}
			else
			{
				lost += 1;
			}
		}

		return new ResultBreakdownViewModel
		{
			Played = played,
			Won = won,
			Drawn = drawn,
			Lost = lost,
			GoalsFor = goalsFor,
			GoalsAgainst = goalsAgainst,
			GoalDifference = goalsFor - goalsAgainst,
			WinPercentage = played > 0 ? Math.Round((double)won / played * 100, 1) : 0,
			AverageGoalsFor = played > 0 ? Math.Round((double)goalsFor / played, 2) : 0,
			AverageGoalsAgainst = played > 0 ? Math.Round((double)goalsAgainst / played, 2) : 0
		};
	}

	private static (int For, int Against) GetClubGoals(Match match)
	{
		if (match.Result is null)
		{
			return (0, 0);
		}

		return match.Venue == MatchVenue.Home
			? (match.Result.HomeGoals, match.Result.AwayGoals)
			: (match.Result.AwayGoals, match.Result.HomeGoals);
	}
}
