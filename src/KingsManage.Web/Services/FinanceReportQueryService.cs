using KingsManage.Web.Models;

namespace KingsManage.Web.Services;

public sealed class FinanceReportQueryService : IFinanceReportQueryService
{
	private readonly IFinanceService financeService;
	private readonly IPlayerService playerService;
	private readonly ISeasonService seasonService;

	public FinanceReportQueryService(
		IFinanceService financeService,
		IPlayerService playerService,
		ISeasonService seasonService)
	{
		this.financeService = financeService;
		this.playerService = playerService;
		this.seasonService = seasonService;
	}

	public async Task<FinanceReportViewModel?> GetAsync(
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

		return BuildReport(financeRows, transactions, season);
	}

	private static FinanceReportViewModel BuildReport(
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
				.GroupBy(transaction => ReportDate.MonthStart(transaction.TransactionDate))
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
}
