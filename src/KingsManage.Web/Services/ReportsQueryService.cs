using KingsManage.Web.Models;

namespace KingsManage.Web.Services;

public sealed class ReportsQueryService : IReportsQueryService
{
	private static readonly ClubEventType[] EventTypes =
	[
		ClubEventType.Match,
		ClubEventType.Training,
		ClubEventType.Social,
		ClubEventType.Meeting
	];

	private readonly IClubEventService eventService;
	private readonly IFinanceService financeService;
	private readonly IMatchService matchService;
	private readonly IPlayerStatsQueryService playerStatsQueryService;
	private readonly IPlayerService playerService;
	private readonly ISeasonService seasonService;

	public ReportsQueryService(
		IClubEventService eventService,
		IFinanceService financeService,
		IMatchService matchService,
		IPlayerStatsQueryService playerStatsQueryService,
		IPlayerService playerService,
		ISeasonService seasonService)
	{
		this.eventService = eventService;
		this.financeService = financeService;
		this.matchService = matchService;
		this.playerStatsQueryService = playerStatsQueryService;
		this.playerService = playerService;
		this.seasonService = seasonService;
	}

	public async Task<AvailabilityReportViewModel?> GetAvailabilityAsync(
		Guid seasonId,
		ClubEventType? eventType,
		CancellationToken cancellationToken = default)
	{
		var season = await seasonService.GetByIdAsync(seasonId, cancellationToken);

		if (season is null)
		{
			return null;
		}

		var completedEvents = await GetCompletedEventsForSeasonAsync(
			season,
			cancellationToken,
			eventType);

		return BuildAvailabilityReport(completedEvents);
	}

	public async Task<TeamPerformanceReportViewModel> GetTeamPerformanceAsync(
		ReportFilters filters,
		CancellationToken cancellationToken = default)
	{
		var completedMatches = await GetFilteredCompletedMatchesAsync(
			filters,
			cancellationToken);

		return BuildTeamPerformanceReport(completedMatches);
	}

	public async Task<OverviewReportViewModel?> GetOverviewAsync(
		ReportFilters filters,
		CancellationToken cancellationToken = default)
	{
		var season = await seasonService.GetByIdAsync(filters.SeasonId, cancellationToken);

		if (season is null)
		{
			return null;
		}

		var players = await playerService.GetAllAsync(cancellationToken);
		var statsRows = await playerStatsQueryService.BuildRowsAsync(
			filters.SeasonId,
			includeFriendlies: true,
			cancellationToken);
		var matches = await GetFilteredCompletedMatchesAsync(
			filters,
			cancellationToken);
		var completedEvents = await GetCompletedEventsForSeasonAsync(
			season,
			cancellationToken);

		return new OverviewReportViewModel
		{
			TeamPerformance = BuildTeamPerformanceReport(matches),
			Availability = BuildAvailabilityReport(completedEvents),
			ActivePlayers = players.Count(player => player.IsActive),
			TopContributors = BuildTopContributors(statsRows, 5)
		};
	}

	public async Task<PlayerReportsViewModel> GetPlayerReportsAsync(
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

		rows = rows
			.Where(row => playerId is null || row.PlayerId == playerId.Value)
			.Where(row =>
				teamId is null ||
				row.TeamStats.Any(stats => stats.TeamId == teamId.Value))
			.ToList();

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

	public async Task<FinanceReportViewModel?> GetFinanceAsync(
		Guid seasonId,
		CancellationToken cancellationToken = default)
	{
		var season = await seasonService.GetByIdAsync(seasonId, cancellationToken);

		if (season is null)
		{
			return null;
		}

		var players = await playerService.GetAllAsync(cancellationToken);
		var transactions = await financeService.GetSeasonTransactionsAsync(
			seasonId,
			cancellationToken);
		var activePlayers = players.Where(player => player.IsActive).ToList();
		var financeRows = activePlayers
			.Select(player => PlayerFinanceViewModel.FromPlayer(player, seasonId, transactions))
			.ToList();

		return BuildFinanceReport(financeRows, transactions, season);
	}

	private async Task<List<ClubEvent>> GetCompletedEventsForSeasonAsync(
		Season season,
		CancellationToken cancellationToken,
		ClubEventType? eventType = null)
	{
		var events = await eventService.GetAllAsync(cancellationToken);

		return events
			.Where(clubEvent =>
				clubEvent.StartDateTime <= DateTime.UtcNow &&
				clubEvent.StartDateTime >= season.StartDate &&
				clubEvent.StartDateTime <= season.EndDate &&
				(eventType is null || clubEvent.Type == eventType))
			.ToList();
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

		return NormaliseCompetition(match.Competition)
			.Equals(competition, StringComparison.OrdinalIgnoreCase);
	}

	private static string NormaliseCompetition(string? competition)
	{
		return string.IsNullOrWhiteSpace(competition)
			? "No competition"
			: competition.Trim();
	}

	private static AvailabilityReportViewModel BuildAvailabilityReport(
		IReadOnlyList<ClubEvent> completedEvents)
	{
		var totals = BuildTotals(completedEvents);
		var totalResponses = (int)(totals.Available + totals.Declined + totals.Unanswered);

		return new AvailabilityReportViewModel
		{
			CompletedEvents = completedEvents.Count,
			TotalResponses = totalResponses,
			AvailablePercentage = totalResponses > 0
				? (int)Math.Round(totals.Available / totalResponses * 100)
				: 0,
			Totals = totals,
			Averages = BuildAverages(totals, completedEvents.Count),
			EventTypes = EventTypes
				.Select(type =>
				{
					var typeEvents = completedEvents
						.Where(clubEvent => clubEvent.Type == type)
						.ToList();
					var typeTotals = BuildTotals(typeEvents);

					return new EventTypeAvailabilityBreakdownViewModel
					{
						Type = type,
						CompletedEvents = typeEvents.Count,
						Totals = typeTotals,
						Averages = BuildAverages(typeTotals, typeEvents.Count)
					};
				})
				.ToList(),
			Months = completedEvents
				.GroupBy(clubEvent => MonthStart(clubEvent.StartDateTime))
				.OrderBy(group => group.Key)
				.Select(group =>
				{
					var monthEvents = group.ToList();
					var monthTotals = BuildTotals(monthEvents);

					return new MonthlyAvailabilityBreakdownViewModel
					{
						Label = group.Key.ToString("MMM"),
						MonthStart = group.Key,
						CompletedEvents = monthEvents.Count,
						Totals = monthTotals,
						Averages = BuildAverages(monthTotals, monthEvents.Count)
					};
				})
				.ToList()
		};
	}

	private static AvailabilityStatusBreakdownViewModel BuildTotals(
		IEnumerable<ClubEvent> events)
	{
		var responses = events
			.SelectMany(clubEvent => clubEvent.AvailabilityResponses)
			.ToList();

		return new AvailabilityStatusBreakdownViewModel
		{
			Available = responses.Count(response => response.Status == ClubEventAvailabilityStatus.Available),
			Declined = responses.Count(response => response.Status == ClubEventAvailabilityStatus.Declined),
			Unanswered = responses.Count(response => response.Status == ClubEventAvailabilityStatus.Unanswered)
		};
	}

	private static AvailabilityStatusBreakdownViewModel BuildAverages(
		AvailabilityStatusBreakdownViewModel totals,
		int eventCount)
	{
		return new AvailabilityStatusBreakdownViewModel
		{
			Available = Average(totals.Available, eventCount),
			Declined = Average(totals.Declined, eventCount),
			Unanswered = Average(totals.Unanswered, eventCount)
		};
	}

	private static double Average(double total, int count)
	{
		return count > 0 ? Math.Round(total / count, 1) : 0;
	}

	private static TeamPerformanceReportViewModel BuildTeamPerformanceReport(
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
				.GroupBy(match => MonthStart(match.Date))
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
				.GroupBy(match => NormaliseCompetition(match.Competition))
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

	private static FinanceReportViewModel BuildFinanceReport(
		IReadOnlyList<PlayerFinanceViewModel> rows,
		IReadOnlyList<FinanceTransaction> transactions,
		Season season)
	{
		var expected = rows.Sum(row => row.AmountOwed);
		var outstanding = rows.Sum(row => row.Balance);
		var now = DateTime.UtcNow;
		var seasonLength = Math.Max(1, (season.EndDate - season.StartDate).TotalDays);
		var elapsedDays = Math.Clamp((now - season.StartDate).TotalDays, 0, seasonLength);
		var remainingDays = Math.Max(1, (season.EndDate - now).TotalDays);
		var elapsedRatio = elapsedDays / seasonLength;
		var paymentTransactions = transactions
			.Where(transaction => transaction.Type == FinanceTransactionType.Payment)
			.ToList();
		var adjustmentTransactions = transactions
			.Where(transaction => transaction.Type == FinanceTransactionType.Adjustment)
			.ToList();
		var collected = paymentTransactions.Sum(transaction => transaction.Amount);
		var adjustments = adjustmentTransactions.Sum(transaction => transaction.Amount);
		var projectedCollected = elapsedRatio > 0
			? Math.Min(expected, collected / (decimal)elapsedRatio)
			: collected;
		var last30DaysCollected = SumPaymentsSince(paymentTransactions, now.AddDays(-30));
		var last90DaysCollected = SumPaymentsSince(paymentTransactions, now.AddDays(-90));
		var dailyPace = elapsedDays > 0 ? collected / (decimal)Math.Max(1, elapsedDays) : collected;
		var requiredDailyPace = outstanding / (decimal)remainingDays;
		var last30DaysPace = last30DaysCollected / 30;
		var last90DaysPace = last90DaysCollected / 90;
		var forecastScenarios = BuildFinanceForecastScenarios(
			expected,
			collected,
			outstanding,
			dailyPace,
			last30DaysPace,
			last90DaysPace,
			requiredDailyPace,
			remainingDays);

		return new FinanceReportViewModel
		{
			Expected = expected,
			Collected = collected,
			Outstanding = outstanding,
			Adjustments = adjustments,
			PaidPercentage = expected > 0 ? (int)Math.Round(collected / expected * 100) : 0,
			PlayersOwing = rows.Count(row => row.Balance > 0),
			ProjectedCollected = projectedCollected,
			ProjectedShortfall = Math.Max(0, expected - projectedCollected),
			DailyPace = dailyPace,
			RequiredDailyPace = requiredDailyPace,
			ElapsedPercentage = (int)Math.Round(elapsedRatio * 100),
			ForecastStatus = BuildFinanceForecastStatus(expected, outstanding, dailyPace, requiredDailyPace),
			Last30DaysCollected = last30DaysCollected,
			Last90DaysCollected = last90DaysCollected,
			Last30DaysPace = last30DaysPace,
			Last90DaysPace = last90DaysPace,
			DaysRemaining = (int)Math.Ceiling(remainingDays),
			ForecastScenarios = forecastScenarios,
			Months = transactions
				.GroupBy(transaction => MonthStart(transaction.TransactionDate))
				.OrderBy(group => group.Key)
				.Select(group => new MonthlyFinanceBreakdownViewModel
				{
					Label = group.Key.ToString("MMM"),
					MonthStart = group.Key,
					Collected = group
						.Where(transaction => transaction.Type == FinanceTransactionType.Payment)
						.Sum(transaction => transaction.Amount),
					Charged = group
						.Where(transaction => transaction.Type == FinanceTransactionType.Charge)
						.Sum(transaction => transaction.Amount),
					Adjustments = group
						.Where(transaction => transaction.Type == FinanceTransactionType.Adjustment)
						.Sum(transaction => transaction.Amount)
				})
				.ToList()
		};
	}

	private static decimal SumPaymentsSince(
		IEnumerable<FinanceTransaction> paymentTransactions,
		DateTime startDate)
	{
		return paymentTransactions
			.Where(transaction => transaction.TransactionDate >= startDate)
			.Sum(transaction => transaction.Amount);
	}

	private static string BuildFinanceForecastStatus(
		decimal expected,
		decimal outstanding,
		decimal currentDailyPace,
		decimal requiredDailyPace)
	{
		if (expected <= 0)
		{
			return "No target";
		}

		if (outstanding <= 0)
		{
			return "On target";
		}

		if (requiredDailyPace <= 0 || currentDailyPace >= requiredDailyPace)
		{
			return "On pace";
		}

		return currentDailyPace >= requiredDailyPace * 0.75m
			? "Needs attention"
			: "Behind pace";
	}

	private static List<FinanceForecastScenarioViewModel> BuildFinanceForecastScenarios(
		decimal expected,
		decimal collected,
		decimal outstanding,
		decimal seasonDailyPace,
		decimal last30DaysPace,
		decimal last90DaysPace,
		decimal requiredDailyPace,
		double remainingDays)
	{
		var scenarios = new[]
		{
			("Season pace", "Continues at the average daily collection rate across this season.", seasonDailyPace),
			("Last 90 days", "Continues at the average daily collection rate from the last 90 days.", last90DaysPace),
			("Last 30 days", "Continues at the average daily collection rate from the last 30 days.", last30DaysPace),
			("Required pace", "Collects exactly enough per day to clear the current outstanding balance.", requiredDailyPace)
		};

		return scenarios
			.Select(scenario =>
			{
				var projectedCollected = Math.Min(
					expected,
					collected + scenario.Item3 * (decimal)Math.Max(0, remainingDays));

				return new FinanceForecastScenarioViewModel
				{
					Label = scenario.Item1,
					Description = scenario.Item2,
					DailyPace = scenario.Item3,
					ProjectedCollected = projectedCollected,
					ProjectedShortfall = Math.Max(0, expected - projectedCollected),
					CompletionPercentage = expected > 0
						? (int)Math.Round(projectedCollected / expected * 100)
						: 0
				};
			})
			.Where(scenario => outstanding > 0 || scenario.ProjectedCollected > 0)
			.ToList();
	}

	private static DateTime MonthStart(DateTime date)
	{
		return new DateTime(
			date.Year,
			date.Month,
			1,
			0,
			0,
			0,
			DateTimeKind.Utc);
	}
}
