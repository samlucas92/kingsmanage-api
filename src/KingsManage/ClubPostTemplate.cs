namespace KingsManage;

public class ClubPostTemplate : ITenantOwned
{
	public Guid OrganizationId { get; set; }
	public Guid ClubId { get; set; }
	public Guid Id { get; set; }
	public string Name { get; set; } = string.Empty;
	public string TitleTemplate { get; set; } = "{{team}} vs {{opponent}}";
	public string BodyTemplate { get; set; } =
		"{{team}}\n\n{{date}}\n{{venue}}\n\nSquad:\n{{squad}}\n\n{{directions}}";
	public bool IsPinned { get; set; }
	public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
	public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
