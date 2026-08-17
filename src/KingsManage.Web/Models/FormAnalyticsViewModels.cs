using KingsManage;

namespace KingsManage.Web.Models;

public sealed class FormAnalyticsTrackModel
{
	public Guid SessionId { get; set; }
	public Guid? FieldId { get; set; }
	public long EngagedDurationMs { get; set; }
	public string ErrorType { get; set; } = string.Empty;
}

public sealed class FormAnalyticsOverviewViewModel
{
	public DateTime? From { get; set; }
	public DateTime? To { get; set; }
	public int TotalViews { get; set; }
	public int UniqueVisitors { get; set; }
	public int Starts { get; set; }
	public int Submissions { get; set; }
	public double CompletionRate { get; set; }
	public double ViewConversionRate { get; set; }
	public List<FormAnalyticsPerformance> Forms { get; set; } = [];
	public List<FormAnalyticsPerformance> NeedsAttention { get; set; } = [];

	public static FormAnalyticsOverviewViewModel FromReport(FormAnalyticsOverview report) => new()
	{
		From = report.From, To = report.To, TotalViews = report.TotalViews,
		UniqueVisitors = report.UniqueVisitors, Starts = report.Starts, Submissions = report.Submissions,
		CompletionRate = report.CompletionRate, ViewConversionRate = report.ViewConversionRate,
		Forms = report.Forms, NeedsAttention = report.NeedsAttention
	};
}

public sealed class FormAnalyticsDetailViewModel
{
	public FormAnalyticsDetail Analytics { get; set; } = new();
	public static FormAnalyticsDetailViewModel FromReport(FormAnalyticsDetail report) => new() { Analytics = report };
}
