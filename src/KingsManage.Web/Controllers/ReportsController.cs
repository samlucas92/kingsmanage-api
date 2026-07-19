using KingsManage;
using KingsManage.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KingsManage.Web.Controllers;

[ApiController]
[Authorize(Policy = "TeamManagement")]
[Route("api/reports")]
public class ReportsController : ControllerBase
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
	private readonly IPlayerService playerService;
	private readonly ISeasonService seasonService;
	private readonly IStatsService statsService;

	public ReportsController(
		IClubEventService eventService,
		IFinanceService financeService,
		IMatchService matchService,
		IPlayerService playerService,
		ISeasonService seasonService,
		IStatsService statsService)
	{
		this.eventService = eventService;
		this.financeService = financeService;
		this.matchService = matchService;
		this.playerService = playerService;
		this.seasonService = seasonService;
		this.statsService = statsService;
	}

	[HttpGet("availability")]
	public async Task<ActionResult<AvailabilityReportViewModel>> GetAvailability(
		[FromQuery] string seasonId,
		[FromQuery] ClubEventType? eventType,
		CancellationToken cancellationToken)
	{
		if (!TryParseGuid(seasonId, "Season", out var parsedSeasonId, out var errorResult))
		{
			return errorResult!;
		}

		var season = await seasonService.GetByIdAsync(parsedSeasonId, cancellationToken);

		if (season is null)
		{
			return NotFound("Season not found.");
		}

		var events = await eventService.GetAllAsync(cancellationToken);
		var completedEvents = events
			.Where(clubEvent =>
				clubEvent.StartDateTime <= DateTime.UtcNow &&
				clubEvent.StartDateTime >= season.StartDate &&
				clubEvent.StartDateTime <= season.EndDate &&
				(eventType is null || clubEvent.Type == eventType))
			.ToList();

		return Ok(BuildAvailabilityReport(completedEvents));
	}

	[HttpGet("team-performance")]
	public async Task<ActionResult<TeamPerformanceReportViewModel>> GetTeamPerformance(
		[FromQuery] string seasonId,
		[FromQuery] string? teamId,
		[FromQuery] string? competition,
		[FromQuery] MatchVenue? venue,
		[FromQuery] DateTime? dateFrom,
		[FromQuery] DateTime? dateTo,
		CancellationToken cancellationToken)
	{
		if (!TryParseGuid(seasonId, "Season", out var parsedSeasonId, out var errorResult))
		{
			return errorResult!;
		}

		Guid? parsedTeamId = null;
		if (!string.IsNullOrWhiteSpace(teamId) && !teamId.Equals("all", StringComparison.OrdinalIgnoreCase))
		{
			if (!Guid.TryParse(teamId, out var teamGuid))
			{
				return BadRequest("Team id must be a valid GUID.");
			}

			parsedTeamId = teamGuid;
		}

		var matches = await matchService.GetBySeasonAsync(parsedSeasonId, cancellationToken);
		var completedMatches = matches
			.Where(match =>
				match.IsCompleted &&
				match.State != MatchState.Postponed &&
				match.Result is not null &&
				(parsedTeamId is null || (match.TeamId ?? DefaultClubTeams.FromLegacy(match.Team)) == parsedTeamId.Value) &&
				(string.IsNullOrWhiteSpace(competition) ||
					competition.Equals("all", StringComparison.OrdinalIgnoreCase) ||
					(match.Competition.Trim().Length == 0
						? "No competition"
						: match.Competition.Trim()).Equals(competition, StringComparison.OrdinalIgnoreCase)) &&
				(venue is null || match.Venue == venue.Value) &&
				(dateFrom is null || match.Date >= dateFrom.Value.Date) &&
				(dateTo is null || match.Date <= dateTo.Value.Date.AddDays(1).AddTicks(-1)))
			.OrderBy(match => match.Date)
			.ToList();

		return Ok(BuildTeamPerformanceReport(completedMatches));
	}

	[HttpGet("overview")]
	public async Task<ActionResult<OverviewReportViewModel>> GetOverview(
		[FromQuery] string seasonId,
		[FromQuery] string? teamId,
		[FromQuery] string? competition,
		[FromQuery] MatchVenue? venue,
		[FromQuery] DateTime? dateFrom,
		[FromQuery] DateTime? dateTo,
		CancellationToken cancellationToken)
	{
		if (!TryParseGuid(seasonId, "Season", out var parsedSeasonId, out var errorResult))
		{
			return errorResult!;
		}

		var season = await seasonService.GetByIdAsync(parsedSeasonId, cancellationToken);

		if (season is null)
		{
			return NotFound("Season not found.");
		}

		var players = await playerService.GetAllAsync(cancellationToken);
		var statsRows = await BuildPlayerStatsRows(parsedSeasonId, cancellationToken);
		var matches = await GetFilteredCompletedMatches(
			parsedSeasonId,
			teamId,
			competition,
			venue,
			dateFrom,
			dateTo,
			cancellationToken);
		var events = await eventService.GetAllAsync(cancellationToken);
		var completedEvents = events
			.Where(clubEvent =>
				clubEvent.StartDateTime <= DateTime.UtcNow &&
				clubEvent.StartDateTime >= season.StartDate &&
				clubEvent.StartDateTime <= season.EndDate)
			.ToList();

		return Ok(new OverviewReportViewModel
		{
			TeamPerformance = BuildTeamPerformanceReport(matches),
			Availability = BuildAvailabilityReport(completedEvents),
			ActivePlayers = players.Count(player => player.IsActive),
			TopContributors = BuildTopContributors(statsRows, 5)
		});
	}

	[HttpGet("players")]
	public async Task<ActionResult<PlayerReportsViewModel>> GetPlayerReports(
		[FromQuery] string seasonId,
		[FromQuery] string? teamId,
		[FromQuery] string? playerId,
		CancellationToken cancellationToken,
		[FromQuery] bool includeFriendlies = true)
	{
		if (!TryParseGuid(seasonId, "Season", out var parsedSeasonId, out var errorResult))
		{
			return errorResult!;
		}

		Guid? parsedTeamId = ParseOptionalGuid(teamId);
		Guid? parsedPlayerId = ParseOptionalGuid(playerId);
		var rows = await BuildPlayerStatsRows(
			parsedSeasonId,
			cancellationToken,
			includeFriendlies);

		rows = rows
			.Where(row => parsedPlayerId is null || row.PlayerId == parsedPlayerId.Value)
			.Where(row =>
				parsedTeamId is null ||
				row.TeamStats.Any(stats => stats.TeamId == parsedTeamId.Value))
			.ToList();

		var activeRows = rows.Where(row => row.IsActive).ToList();

		return Ok(new PlayerReportsViewModel
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
			SquadUsage = BuildSquadUsage(activeRows, parsedTeamId),
			Discipline = BuildDisciplineReport(rows)
		});
	}

	[Authorize(Policy = "ClubAdmin")]
	[HttpGet("finance")]
	public async Task<ActionResult<FinanceReportViewModel>> GetFinance(
		[FromQuery] string seasonId,
		CancellationToken cancellationToken)
	{
		if (!TryParseGuid(seasonId, "Season", out var parsedSeasonId, out var errorResult))
		{
			return errorResult!;
		}

		var season = await seasonService.GetByIdAsync(parsedSeasonId, cancellationToken);

		if (season is null)
		{
			return NotFound("Season not found.");
		}

		var players = await playerService.GetAllAsync(cancellationToken);
		var transactions = await financeService.GetSeasonTransactionsAsync(
			parsedSeasonId,
			cancellationToken);
		var activePlayers = players.Where(player => player.IsActive).ToList();
		var financeRows = activePlayers
			.Select(player => PlayerFinanceViewModel.FromPlayer(player, parsedSeasonId, transactions))
			.ToList();

		return Ok(BuildFinanceReport(financeRows, transactions, season));
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
				.GroupBy(clubEvent => new DateTime(
					clubEvent.StartDateTime.Year,
					clubEvent.StartDateTime.Month,
					1,
					0,
					0,
					0,
					DateTimeKind.Utc))
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
			.SelectMany(clubEvent => clubEvent.AvailabilityResponses);

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
				.GroupBy(match => new DateTime(
					match.Date.Year,
					match.Date.Month,
					1,
					0,
					0,
					0,
					DateTimeKind.Utc))
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
				.Select(match => BuildMatchHighlight(match))
				.Where(match => match.Margin > 0)
				.OrderByDescending(match => match.Margin)
				.ThenByDescending(match => match.GoalsFor)
				.FirstOrDefault(),
			BiggestLoss = completedMatches
				.Select(match => BuildMatchHighlight(match))
				.Where(match => match.Margin < 0)
				.OrderBy(match => match.Margin)
				.ThenByDescending(match => match.GoalsAgainst)
				.FirstOrDefault(),
			Competitions = completedMatches
				.GroupBy(match => string.IsNullOrWhiteSpace(match.Competition)
					? "No competition"
					: match.Competition.Trim())
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

	private async Task<List<Match>> GetFilteredCompletedMatches(
		Guid seasonId,
		string? teamId,
		string? competition,
		MatchVenue? venue,
		DateTime? dateFrom,
		DateTime? dateTo,
		CancellationToken cancellationToken)
	{
		var parsedTeamId = ParseOptionalGuid(teamId);
		var matches = await matchService.GetBySeasonAsync(seasonId, cancellationToken);

		return matches
			.Where(match =>
				match.IsCompleted &&
				match.State != MatchState.Postponed &&
				match.Result is not null &&
				(parsedTeamId is null || (match.TeamId ?? DefaultClubTeams.FromLegacy(match.Team)) == parsedTeamId.Value) &&
				(string.IsNullOrWhiteSpace(competition) ||
					competition.Equals("all", StringComparison.OrdinalIgnoreCase) ||
					(match.Competition.Trim().Length == 0
						? "No competition"
						: match.Competition.Trim()).Equals(competition, StringComparison.OrdinalIgnoreCase)) &&
				(venue is null || match.Venue == venue.Value) &&
				(dateFrom is null || match.Date >= dateFrom.Value.Date) &&
				(dateTo is null || match.Date <= dateTo.Value.Date.AddDays(1).AddTicks(-1)))
			.OrderBy(match => match.Date)
			.ToList();
	}

	private async Task<List<PlayerStatsViewModel>> BuildPlayerStatsRows(
		Guid seasonId,
		CancellationToken cancellationToken,
		bool includeFriendlies = true)
	{
		var players = await playerService.GetAllAsync(cancellationToken);
		List<PlayerSeasonStats> selectedSeasonStats;
		List<PlayerSeasonStats> allSeasonStats;

		if (includeFriendlies)
		{
			selectedSeasonStats = await statsService.GetSeasonStatsAsync(
				seasonId,
				cancellationToken);
			allSeasonStats = await statsService.GetAllSeasonStatsAsync(cancellationToken);
		}
		else
		{
			var allMatches = await matchService.GetAllAsync(cancellationToken);
			var competitiveMatches = allMatches
				.Where(match => !MatchCompetition.IsFriendly(match.Competition))
				.ToList();

			selectedSeasonStats = SeasonStatsCalculator.Calculate(
				seasonId,
				competitiveMatches);
			allSeasonStats = competitiveMatches
				.Where(match => match.SeasonId is not null)
				.Select(match => match.SeasonId!.Value)
				.Distinct()
				.SelectMany(matchSeasonId => SeasonStatsCalculator.Calculate(
					matchSeasonId,
					competitiveMatches))
				.ToList();
		}

		var historicalStats = await statsService.GetHistoricalStatsAsync(cancellationToken);

		return players
			.OrderBy(player => player.Name)
			.Select(player => PlayerStatsViewModel.FromStats(
				player,
				selectedSeasonStats,
				allSeasonStats,
				historicalStats.FirstOrDefault(stats => stats.PlayerId == player.Id)))
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
				.GroupBy(transaction => new DateTime(
					transaction.TransactionDate.Year,
					transaction.TransactionDate.Month,
					1,
					0,
					0,
					0,
					DateTimeKind.Utc))
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

	private static Guid? ParseOptionalGuid(string? id)
	{
		if (string.IsNullOrWhiteSpace(id) || id.Equals("all", StringComparison.OrdinalIgnoreCase))
		{
			return null;
		}

		return Guid.TryParse(id, out var parsedId) ? parsedId : null;
	}

	private bool TryParseGuid(
		string id,
		string entityName,
		out Guid parsedId,
		out BadRequestObjectResult? errorResult)
	{
		parsedId = Guid.Empty;
		errorResult = null;

		if (string.IsNullOrWhiteSpace(id))
		{
			errorResult = BadRequest($"{entityName} id is required.");
			return false;
		}

		if (!Guid.TryParse(id, out parsedId))
		{
			errorResult = BadRequest($"{entityName} id must be a valid GUID.");
			return false;
		}

		return true;
	}
}
