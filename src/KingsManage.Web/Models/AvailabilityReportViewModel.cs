using KingsManage;

namespace KingsManage.Web.Models;

public sealed class AvailabilityReportViewModel
{
	public int CompletedEvents { get; set; }
	public int TotalResponses { get; set; }
	public int AvailablePercentage { get; set; }
	public AvailabilityStatusBreakdownViewModel Totals { get; set; } = new();
	public AvailabilityStatusBreakdownViewModel Averages { get; set; } = new();
	public List<EventTypeAvailabilityBreakdownViewModel> EventTypes { get; set; } = [];
	public List<MonthlyAvailabilityBreakdownViewModel> Months { get; set; } = [];
}

public sealed class AvailabilityStatusBreakdownViewModel
{
	public double Available { get; set; }
	public double Declined { get; set; }
	public double Unanswered { get; set; }
}

public sealed class EventTypeAvailabilityBreakdownViewModel
{
	public ClubEventType Type { get; set; }
	public int CompletedEvents { get; set; }
	public AvailabilityStatusBreakdownViewModel Totals { get; set; } = new();
	public AvailabilityStatusBreakdownViewModel Averages { get; set; } = new();
}

public sealed class MonthlyAvailabilityBreakdownViewModel
{
	public string Label { get; set; } = string.Empty;
	public DateTime MonthStart { get; set; }
	public int CompletedEvents { get; set; }
	public AvailabilityStatusBreakdownViewModel Totals { get; set; } = new();
	public AvailabilityStatusBreakdownViewModel Averages { get; set; } = new();
}
