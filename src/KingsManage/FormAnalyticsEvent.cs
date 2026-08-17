namespace KingsManage;

public enum FormAnalyticsEventType
{
	Viewed,
	InteractionStarted,
	Submitted,
	FieldInteracted,
	ValidationError
}

public sealed class FormAnalyticsEvent : ITenantOwned
{
	public Guid OrganizationId { get; set; }
	public Guid ClubId { get; set; }
	public Guid Id { get; set; }
	public Guid FormId { get; set; }
	public FormAnalyticsEventType EventType { get; set; }
	public Guid? UserId { get; set; }
	public Guid SessionId { get; set; }
	public DateTime OccurredAt { get; set; } = DateTime.UtcNow;
	public long? DurationMs { get; set; }
	public Guid? FieldId { get; set; }
	public string ErrorType { get; set; } = string.Empty;
}
