namespace KingsManage.Web.Models;

public sealed class TeamPerformanceReportViewModel
{
	public ResultBreakdownViewModel Summary { get; set; } = new();
	public HomeAwayBreakdownViewModel HomeAway { get; set; } = new();
	public List<MonthlyResultBreakdownViewModel> Months { get; set; } = [];
	public List<string> RecentForm { get; set; } = [];
}

public sealed class ResultBreakdownViewModel
{
	public int Played { get; set; }
	public int Won { get; set; }
	public int Drawn { get; set; }
	public int Lost { get; set; }
	public int GoalsFor { get; set; }
	public int GoalsAgainst { get; set; }
	public int GoalDifference { get; set; }
	public double WinPercentage { get; set; }
	public double AverageGoalsFor { get; set; }
	public double AverageGoalsAgainst { get; set; }
}

public sealed class HomeAwayBreakdownViewModel
{
	public ResultBreakdownViewModel Home { get; set; } = new();
	public ResultBreakdownViewModel Away { get; set; } = new();
}

public sealed class MonthlyResultBreakdownViewModel
{
	public string Label { get; set; } = string.Empty;
	public DateTime MonthStart { get; set; }
	public int Wins { get; set; }
	public int Draws { get; set; }
	public int Losses { get; set; }
	public int GoalsFor { get; set; }
	public int GoalsAgainst { get; set; }
}
