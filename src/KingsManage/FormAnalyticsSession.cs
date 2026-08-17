namespace KingsManage;

public sealed class FormAnalyticsSession : ITenantOwned
{
	public Guid OrganizationId { get; set; }
	public Guid ClubId { get; set; }
	public Guid Id { get; set; }
	public Guid FormId { get; set; }
	public Guid? UserId { get; set; }
	public DateTime StartedAt { get; set; } = DateTime.UtcNow;
	public DateTime LastActivityAt { get; set; } = DateTime.UtcNow;
	public long EngagedDurationMs { get; set; }
	public bool HasInteracted { get; set; }
	public DateTime? SubmittedAt { get; set; }
}
