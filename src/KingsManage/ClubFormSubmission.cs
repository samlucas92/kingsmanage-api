namespace KingsManage;

public class ClubFormSubmission : ITenantOwned
{
	public Guid OrganizationId { get; set; }
	public Guid ClubId { get; set; }
	public Guid Id { get; set; }
	public Guid FormId { get; set; }
	public Guid SubmittedByUserId { get; set; }
	public string RespondentKey { get; set; } = string.Empty;
	public string SubmissionLimitKey { get; set; } = string.Empty;
	public Guid? AnalyticsSessionId { get; set; }
	public List<ClubFormAnswer> Answers { get; set; } = [];
	public DateTime SubmittedAt { get; set; } = DateTime.UtcNow;
}
