namespace KingsManage;

public class ClubPost : ITenantOwned
{
	public Guid OrganizationId { get; set; }
	public Guid ClubId { get; set; }
	public Guid Id { get; set; }
	public ClubPostType Type { get; set; } = ClubPostType.General;
	public string Title { get; set; } = string.Empty;
	public string Body { get; set; } = string.Empty;
	public bool IsPinned { get; set; }
	public Guid CreatedByUserId { get; set; }
	public string CreatedByUserEmail { get; set; } = string.Empty;
	public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
	public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
