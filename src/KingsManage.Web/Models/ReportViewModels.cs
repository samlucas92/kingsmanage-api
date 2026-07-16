namespace KingsManage.Web.Models;

public sealed class OverviewReportViewModel
{
	public TeamPerformanceReportViewModel TeamPerformance { get; set; } = new();
	public AvailabilityReportViewModel Availability { get; set; } = new();
	public FinanceReportViewModel? Finance { get; set; }
	public int ActivePlayers { get; set; }
	public List<PlayerContributionViewModel> TopContributors { get; set; } = [];
}

public sealed class PlayerReportsViewModel
{
	public PlayerStatsSummaryViewModel Summary { get; set; } = new();
	public List<PlayerStatsViewModel> Players { get; set; } = [];
	public List<PlayerContributionViewModel> TopContributors { get; set; } = [];
	public List<PlayerUsageViewModel> SquadUsage { get; set; } = [];
	public DisciplineReportViewModel Discipline { get; set; } = new();
}

public sealed class PlayerStatsSummaryViewModel
{
	public int ActivePlayers { get; set; }
	public int Appearances { get; set; }
	public int Goals { get; set; }
	public int Assists { get; set; }
	public int Contributions { get; set; }
	public int Minutes { get; set; }
}

public sealed class PlayerContributionViewModel
{
	public Guid PlayerId { get; set; }
	public string PlayerName { get; set; } = string.Empty;
	public int Goals { get; set; }
	public int Assists { get; set; }
	public int Contributions { get; set; }
	public int Appearances { get; set; }
}

public sealed class PlayerUsageViewModel
{
	public Guid PlayerId { get; set; }
	public string PlayerName { get; set; } = string.Empty;
	public int Appearances { get; set; }
	public int Starts { get; set; }
	public int Bench { get; set; }
	public int UnusedSubstitutes { get; set; }
	public int Minutes { get; set; }
	public int Goals { get; set; }
	public int Assists { get; set; }
}

public sealed class DisciplineReportViewModel
{
	public int YellowCards { get; set; }
	public int RedCards { get; set; }
	public int TotalCards { get; set; }
	public List<PlayerDisciplineViewModel> Players { get; set; } = [];
}

public sealed class PlayerDisciplineViewModel
{
	public Guid PlayerId { get; set; }
	public string PlayerName { get; set; } = string.Empty;
	public int YellowCards { get; set; }
	public int RedCards { get; set; }
	public int TotalCards { get; set; }
}

public sealed class FinanceReportViewModel
{
	public decimal Expected { get; set; }
	public decimal Collected { get; set; }
	public decimal Outstanding { get; set; }
	public int PaidPercentage { get; set; }
	public int PlayersOwing { get; set; }
	public decimal ProjectedCollected { get; set; }
	public decimal ProjectedShortfall { get; set; }
	public decimal DailyPace { get; set; }
	public decimal RequiredDailyPace { get; set; }
	public int ElapsedPercentage { get; set; }
	public List<MonthlyFinanceBreakdownViewModel> Months { get; set; } = [];
}

public sealed class MonthlyFinanceBreakdownViewModel
{
	public string Label { get; set; } = string.Empty;
	public DateTime MonthStart { get; set; }
	public decimal Collected { get; set; }
	public decimal Charged { get; set; }
}
