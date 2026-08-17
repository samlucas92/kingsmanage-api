namespace KingsManage;

public interface IFormAnalyticsService
{
	Task RecordViewAsync(ClubForm form, Guid sessionId, Guid? userId, CancellationToken cancellationToken = default);
	Task RecordInteractionAsync(ClubForm form, Guid sessionId, Guid? userId, CancellationToken cancellationToken = default);
	Task RecordFieldInteractionAsync(ClubForm form, Guid sessionId, Guid fieldId, Guid? userId, CancellationToken cancellationToken = default);
	Task RecordValidationErrorAsync(ClubForm form, Guid sessionId, Guid? fieldId, string errorType, Guid? userId, CancellationToken cancellationToken = default);
	Task UpdateDurationAsync(ClubForm form, Guid sessionId, long engagedDurationMs, Guid? userId, CancellationToken cancellationToken = default);
	Task RecordSubmissionAsync(ClubForm form, Guid sessionId, Guid? userId, CancellationToken cancellationToken = default);
	Task<FormAnalyticsOverview> GetOverviewAsync(DateTime? from, DateTime? to, CancellationToken cancellationToken = default);
	Task<FormAnalyticsDetail?> GetFormAnalyticsAsync(Guid formId, DateTime? from, DateTime? to, CancellationToken cancellationToken = default);
	Task DeleteForFormAsync(Guid formId, CancellationToken cancellationToken = default);
}

public sealed class FormAnalyticsOverview
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
}

public sealed class FormAnalyticsDetail : FormAnalyticsPerformance
{
	public double ViewToStartRate { get; set; }
	public long MedianEngagedDurationMs { get; set; }
	public long AverageCompletedDurationMs { get; set; }
	public long AverageAbandonedDurationMs { get; set; }
	public List<FormAnalyticsTrendPoint> Trends { get; set; } = [];
	public List<FormFieldAnalytics> Fields { get; set; } = [];
}

public class FormAnalyticsPerformance
{
	public Guid FormId { get; set; }
	public string FormName { get; set; } = string.Empty;
	public int Views { get; set; }
	public int UniqueVisitors { get; set; }
	public int Starts { get; set; }
	public int Submissions { get; set; }
	public int Abandoned { get; set; }
	public double CompletionRate { get; set; }
	public double AbandonmentRate { get; set; }
	public double ViewConversionRate { get; set; }
	public long AverageEngagedDurationMs { get; set; }
	public int ValidationErrors { get; set; }
}

public sealed class FormAnalyticsTrendPoint
{
	public DateTime Date { get; set; }
	public int Views { get; set; }
	public int Starts { get; set; }
	public int Submissions { get; set; }
	public double CompletionRate { get; set; }
}

public sealed class FormFieldAnalytics
{
	public Guid FieldId { get; set; }
	public string FieldName { get; set; } = string.Empty;
	public bool IsRequired { get; set; }
	public int Interactions { get; set; }
	public int ValidationErrors { get; set; }
}

public sealed class FormAnalyticsEventCount
{
	public Guid FormId { get; set; }
	public FormAnalyticsEventType EventType { get; set; }
	public Guid? FieldId { get; set; }
	public int Count { get; set; }
}
