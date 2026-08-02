namespace KingsManage;

public class ClubForm : ITenantOwned
{
	public Guid OrganizationId { get; set; }
	public Guid ClubId { get; set; }
	public Guid Id { get; set; }
	public string GoCode { get; set; } = string.Empty;
	public string Title { get; set; } = string.Empty;
	public string Description { get; set; } = string.Empty;
	public ClubFormStatus Status { get; set; } = ClubFormStatus.Draft;
	public bool AllowAnonymousResponses { get; set; } = true;
	public bool AllowMultipleSubmissions { get; set; }
	public List<ClubFormQuestion> Questions { get; set; } = [];
	public Guid CreatedByUserId { get; set; }
	public string CreatedByUserEmail { get; set; } = string.Empty;
	public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
	public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
